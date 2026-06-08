using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadConcurrencyService(IUploadFilesRepository repository) : IDisposable
{
    private const int MaxParallelUploads = 10;

    private readonly SemaphoreSlim globalUploadSemaphore = new(
        initialCount: MaxParallelUploads,
        maxCount: MaxParallelUploads
    );

    private readonly Dictionary<string, SemaphoreSlim> hosterUploadSemaphores = new();

    private bool disposed;

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

            if (!hosterConfigs.TryGetValue(hosterName, out var serializedConfig))
            {
                continue;
            }

            var hoster = hostersByName[hosterName];
            var hosterConfig = hoster.DeserializeHosterConfig(serializedConfig);

            var maxParallelUploads =
                await hoster.GetMaximumParallelUploadsAsync(hosterConfig, cancellationToken) ?? 1;

            hosterUploadSemaphores[hosterName] = new SemaphoreSlim(maxParallelUploads);
        }
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
