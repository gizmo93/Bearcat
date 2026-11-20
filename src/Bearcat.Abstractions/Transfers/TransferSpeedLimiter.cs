using System.Diagnostics;

namespace Bearcat.Abstractions.Transfers;

public sealed class TransferSpeedLimiter(int bytesPerSecond)
{
    private static readonly TimeSpan ShortestDelay = TimeSpan.FromMilliseconds(5);

    private readonly Lock nextFreeTimestampLock = new();

    private long nextFreeTimestamp;

    public int BytesPerSecond { get; private set; } = bytesPerSecond;

    public async ValueTask WaitForBytesAsync(int byteCount, CancellationToken cancellationToken)
    {
        TimeSpan delay;

        lock (nextFreeTimestampLock)
        {
            var now = Stopwatch.GetTimestamp();
            var transferDuration = byteCount * Stopwatch.Frequency / BytesPerSecond;
            nextFreeTimestamp = Math.Max(nextFreeTimestamp, now) + transferDuration;
            delay = Stopwatch.GetElapsedTime(now, nextFreeTimestamp);
        }

        if (delay >= ShortestDelay)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    public void ChangeBytesPerSecond(int bytesPerSecond)
    {
        lock (nextFreeTimestampLock)
        {
            BytesPerSecond = bytesPerSecond;
            nextFreeTimestamp = 0;
        }
    }

    public void StopLimiting()
    {
        ChangeBytesPerSecond(int.MaxValue);
    }
}
