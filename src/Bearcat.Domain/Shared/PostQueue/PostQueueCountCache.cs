using System.Diagnostics;

namespace Bearcat.Domain.Shared.PostQueue;

public sealed class PostQueueCountCache(TimeSpan maximumCountAge) : IDisposable
{
    public static readonly TimeSpan DefaultMaximumCountAge = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim countingSemaphore = new(1, 1);
    private readonly Lock sync = new();
    private int invalidationCount;
    private CachedOpenItemCount? cachedOpenItemCount;

    public PostQueueCountCache()
        : this(DefaultMaximumCountAge) { }

    public async Task<int> GetOrCountOpenItemsAsync(
        Func<CancellationToken, Task<int>> countOpenItemsAsync,
        CancellationToken cancellationToken
    )
    {
        await countingSemaphore.WaitAsync(cancellationToken);

        try
        {
            int invalidationCountBeforeCounting;

            lock (sync)
            {
                if (
                    cachedOpenItemCount is not null
                    && Stopwatch.GetElapsedTime(cachedOpenItemCount.CountedAtTimestamp)
                        < maximumCountAge
                )
                {
                    return cachedOpenItemCount.OpenItemCount;
                }

                invalidationCountBeforeCounting = invalidationCount;
            }

            var countedAtTimestamp = Stopwatch.GetTimestamp();
            var openItemCount = await countOpenItemsAsync(cancellationToken);

            lock (sync)
            {
                if (invalidationCount == invalidationCountBeforeCounting)
                {
                    cachedOpenItemCount = new CachedOpenItemCount(
                        openItemCount,
                        countedAtTimestamp
                    );
                }
            }

            return openItemCount;
        }
        finally
        {
            countingSemaphore.Release();
        }
    }

    public void Invalidate()
    {
        lock (sync)
        {
            cachedOpenItemCount = null;
            invalidationCount++;
        }
    }

    public void Dispose()
    {
        countingSemaphore.Dispose();
    }

    private sealed record CachedOpenItemCount(int OpenItemCount, long CountedAtTimestamp);
}
