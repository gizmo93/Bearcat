using System.Threading.RateLimiting;

namespace Bearcat.Abstractions.Transfers;

public sealed class TransferSpeedLimiter(int bytesPerSecond) : IDisposable
{
    private static readonly TimeSpan ReplenishmentPeriod = TimeSpan.FromMilliseconds(100);

    private volatile TokenBucketWithRate currentTokenBucket = CreateTokenBucket(bytesPerSecond);

    private volatile bool isDisposed;

    public int BytesPerSecond => currentTokenBucket.BytesPerSecond;

    public int MaximumBytesPerAcquire => currentTokenBucket.BytesPerSecond;

    public async ValueTask AcquireAsync(int byteCount, CancellationToken cancellationToken)
    {
        while (!isDisposed)
        {
            var tokenBucket = currentTokenBucket;
            var permitCount = Math.Min(byteCount, tokenBucket.BytesPerSecond);

            RateLimitLease lease;

            try
            {
                lease = await tokenBucket.RateLimiter.AcquireAsync(permitCount, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                continue;
            }

            using (lease)
            {
                if (lease.IsAcquired)
                {
                    return;
                }
            }
        }
    }

    public void ChangeBytesPerSecond(int bytesPerSecond)
    {
        var previousTokenBucket = Interlocked.Exchange(
            ref currentTokenBucket,
            CreateTokenBucket(bytesPerSecond)
        );
        previousTokenBucket.RateLimiter.Dispose();
    }

    public void Dispose()
    {
        isDisposed = true;
        currentTokenBucket.RateLimiter.Dispose();
    }

    private static TokenBucketWithRate CreateTokenBucket(int bytesPerSecond)
    {
        var rateLimiter = new TokenBucketRateLimiter(
            new TokenBucketRateLimiterOptions
            {
                TokenLimit = bytesPerSecond,
                ReplenishmentPeriod = ReplenishmentPeriod,
                TokensPerPeriod = Math.Max(1, bytesPerSecond / 10),
                QueueLimit = int.MaxValue,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }
        );

        return new TokenBucketWithRate(rateLimiter, bytesPerSecond);
    }

    private sealed record TokenBucketWithRate(
        TokenBucketRateLimiter RateLimiter,
        int BytesPerSecond
    );
}
