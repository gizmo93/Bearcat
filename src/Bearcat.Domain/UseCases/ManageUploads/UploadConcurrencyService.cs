using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadConcurrencyService : IDisposable
{
    private readonly IUploadFilesRepository repository;

    private readonly SemaphoreSlim globalUploadSemaphore;

    private readonly Dictionary<string, SemaphoreSlim> hosterUploadSemaphores = new();

    private bool disposed;

    public UploadConcurrencyService(
        IUploadFilesRepository repository,
        IApplicationConfigurationProvider configuration
    )
    {
        this.repository = repository;

        var maxParallelUploads = Math.Max(
            1,
            configuration.GetValue<UploadConcurrencyConfiguration>(c => c.MaxParallelUploads)
        );

        globalUploadSemaphore = new SemaphoreSlim(
            initialCount: maxParallelUploads,
            maxCount: maxParallelUploads
        );
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

            var maxParallelUploads = await ResolveMaximumParallelUploadsAsync(
                hoster: hoster,
                hosterConfig: hosterConfig,
                maxParallelUploadsOverride: concurrencyInfo.MaxParallelUploadsOverride,
                cancellationToken: cancellationToken
            );

            hosterUploadSemaphores[hosterName] = new SemaphoreSlim(maxParallelUploads);
        }
    }

    private static async Task<int> ResolveMaximumParallelUploadsAsync(
        IHoster hoster,
        IHosterConfig hosterConfig,
        int? maxParallelUploadsOverride,
        CancellationToken cancellationToken
    )
    {
        // Hosters that report their limit via API (e.g. Rapidgator) must not be overridden.
        // The others have just an assumed limit hardcoded and can be overridden
        if (!hoster.HasFixedParallelUploadLimit && maxParallelUploadsOverride is { } overrideValue)
        {
            return Math.Max(1, overrideValue);
        }

        return await hoster.GetMaximumParallelUploadsAsync(hosterConfig, cancellationToken) ?? 1;
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
