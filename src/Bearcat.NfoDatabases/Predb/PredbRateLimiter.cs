using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.NfoDatabases.Predb;

public class PredbRateLimiter
{
    private const int MaxRequestsPerWindow = 25;

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(15);

    private readonly Lock stateLock = new();
    private readonly DateTimeOffset[] recentRequests = new DateTimeOffset[MaxRequestsPerWindow];
    private int oldestIndex;

    public async Task WaitForSlotAsync(CancellationToken cancellationToken = default)
    {
        var giveUpAt = DateTimeOffset.UtcNow + MaxWait;

        while (!TryTakeSlot(out var nextSlotFreeAt))
        {
            if (nextSlotFreeAt > giveUpAt)
            {
                throw new NfoDatabaseRateLimitExceededException("PreDB", nextSlotFreeAt);
            }

            await DelayUntilAsync(nextSlotFreeAt, cancellationToken);
        }
    }

    private bool TryTakeSlot(out DateTimeOffset nextSlotFreeAt)
    {
        lock (stateLock)
        {
            var now = DateTimeOffset.UtcNow;
            nextSlotFreeAt = recentRequests[oldestIndex] + Window;

            if (nextSlotFreeAt > now)
            {
                return false;
            }

            recentRequests[oldestIndex] = now;
            oldestIndex = (oldestIndex + 1) % MaxRequestsPerWindow;
            return true;
        }
    }

    private static Task DelayUntilAsync(DateTimeOffset moment, CancellationToken cancellationToken)
    {
        var delay = moment - DateTimeOffset.UtcNow;
        return Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, cancellationToken);
    }
}
