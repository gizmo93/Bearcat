using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadConcurrencyService(
    IUploadFilesRepository repository,
    IApplicationConfigurationProvider configuration,
    HosterCredentialsRejectionService credentialsRejectionService,
    ILogger<UploadConcurrencyService> logger
) : IDisposable
{
    private readonly SemaphoreSlim globalUploadSemaphore = CreateGlobalUploadSemaphore(
        configuration
    );

    private readonly Dictionary<string, SemaphoreSlim> hosterUploadSemaphores = new();

    private bool disposed;

    public bool HasHosterSemaphore(string hosterClassName)
    {
        return hosterUploadSemaphores.ContainsKey(hosterClassName);
    }

    public async Task<bool> TryAcquireGlobalSlotAsync(CancellationToken cancellationToken)
    {
        return await globalUploadSemaphore.WaitAsync(0, cancellationToken);
    }

    public async Task<(bool Acquired, SemaphoreSlim? Semaphore)> TryAcquireHosterSlotAsync(
        string hosterClassName,
        CancellationToken cancellationToken
    )
    {
        if (
            !hosterUploadSemaphores.TryGetValue(hosterClassName, out var semaphore)
            || !await semaphore.WaitAsync(0, cancellationToken)
        )
        {
            return (false, null);
        }

        return (true, semaphore);
    }

    public void ReleaseGlobalSlot()
    {
        globalUploadSemaphore.Release();
    }

    public async Task EnsureHosterSemaphoresAsync(
        IReadOnlyList<string> hosterClassNames,
        Dictionary<string, IHoster> hostersByName,
        CancellationToken cancellationToken
    )
    {
        if (hosterClassNames.Count == 0 || hosterClassNames.All(hosterUploadSemaphores.ContainsKey))
        {
            return;
        }

        var hosterConfigs = await repository.GetConfigByHosterClassName(cancellationToken);

        foreach (var hosterName in hosterClassNames)
        {
            if (hosterUploadSemaphores.ContainsKey(hosterName))
            {
                continue;
            }

            if (!hosterConfigs.TryGetValue(hosterName, out var concurrencyInfo))
            {
                continue;
            }

            var hoster = hostersByName[hosterName];
            var hosterConfig = hoster.DeserializeHosterConfig(concurrencyInfo.SerializedConfig);

            using var proxyScope = HosterRegistrationProxyScope.EnterForUpload(
                concurrencyInfo.UploadProxySelection,
                concurrencyInfo.UploadProxyServerId
            );

            try
            {
                var maxParallelUploads = await ResolveMaximumParallelUploadsAsync(
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    maxParallelUploadsOverride: concurrencyInfo.MaxParallelUploadsOverride,
                    cancellationToken: cancellationToken
                );

                hosterUploadSemaphores[hosterName] = new SemaphoreSlim(maxParallelUploads);
            }
            catch (HosterCredentialsRejectedException ex)
            {
                var registration = await repository.GetHosterRegistrationByIdAsync(
                    concurrencyInfo.HosterRegistrationId,
                    cancellationToken
                );

                await credentialsRejectionService.DeactivateAndNotifyAsync(
                    registration,
                    ex.Message,
                    cancellationToken
                );
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Failed to determine the maximum number of parallel uploads for hoster {HosterName}, falling back to 1 parallel upload: {Message}",
                    hosterName,
                    ex.Message
                );

                hosterUploadSemaphores[hosterName] = new SemaphoreSlim(1);
            }
        }
    }

    private static async Task<int> ResolveMaximumParallelUploadsAsync(
        IHoster hoster,
        IHosterConfig hosterConfig,
        int? maxParallelUploadsOverride,
        CancellationToken cancellationToken
    )
    {
        if (!hoster.HasFixedParallelUploadLimit && maxParallelUploadsOverride is { } overrideValue)
        {
            return Math.Max(1, overrideValue);
        }

        return await hoster.GetMaximumParallelUploadsAsync(hosterConfig, cancellationToken) ?? 1;
    }

    private static SemaphoreSlim CreateGlobalUploadSemaphore(
        IApplicationConfigurationProvider configuration
    )
    {
        var maxParallelUploads = Math.Max(
            1,
            configuration.GetValue<UploadConcurrencyConfiguration>(c => c.MaxParallelUploads)
        );

        return new SemaphoreSlim(initialCount: maxParallelUploads, maxCount: maxParallelUploads);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            globalUploadSemaphore.Dispose();

            foreach (var semaphore in hosterUploadSemaphores.Values)
            {
                semaphore.Dispose();
            }
        }

        disposed = true;
    }
}
