using System.Collections.Concurrent;
using System.Diagnostics;

namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

public sealed class UploadProgressTracker : IUploadProgressTracker
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<int, UploadSpeedState> states = new();

    public void StartTracking(int uploadId)
    {
        states[uploadId] = new UploadSpeedState(Stopwatch.GetTimestamp());
    }

    public void AddBytes(int uploadId, long bytes)
    {
        if (states.TryGetValue(uploadId, out var state))
        {
            state.AddBytes(bytes, Stopwatch.GetTimestamp(), SampleInterval, SpeedWindow);
        }
    }

    public void StopTracking(int uploadId)
    {
        states.TryRemove(uploadId, out _);
    }

    public UploadSpeedSnapshot? Get(int uploadId)
    {
        if (!states.TryGetValue(uploadId, out var state))
        {
            return null;
        }

        var bytesPerSecond = state.GetBytesPerSecond(Stopwatch.GetTimestamp(), SpeedWindow);

        return new UploadSpeedSnapshot(uploadId, bytesPerSecond);
    }

    private sealed class UploadSpeedState
    {
        private readonly Lock gate = new();

        private readonly Queue<Sample> samples = new();

        private long cumulativeBytes;

        private long lastSampleTimestamp;

        public UploadSpeedState(long startTimestamp)
        {
            lastSampleTimestamp = startTimestamp;
            samples.Enqueue(new Sample(startTimestamp, CumulativeBytes: 0));
        }

        public void AddBytes(
            long bytes,
            long nowTimestamp,
            TimeSpan sampleInterval,
            TimeSpan window
        )
        {
            lock (gate)
            {
                cumulativeBytes += bytes;

                if (Stopwatch.GetElapsedTime(lastSampleTimestamp, nowTimestamp) < sampleInterval)
                {
                    return;
                }

                lastSampleTimestamp = nowTimestamp;
                samples.Enqueue(new Sample(nowTimestamp, cumulativeBytes));
                TrimOldSamples(nowTimestamp, window);
            }
        }

        public double GetBytesPerSecond(long nowTimestamp, TimeSpan window)
        {
            lock (gate)
            {
                TrimOldSamples(nowTimestamp, window);

                var oldest = samples.Peek();
                var elapsedSeconds = Stopwatch
                    .GetElapsedTime(oldest.Timestamp, nowTimestamp)
                    .TotalSeconds;

                if (elapsedSeconds <= 0)
                {
                    return 0;
                }

                return (cumulativeBytes - oldest.CumulativeBytes) / elapsedSeconds;
            }
        }

        private void TrimOldSamples(long nowTimestamp, TimeSpan window)
        {
            while (
                samples.Count > 1
                && Stopwatch.GetElapsedTime(samples.Peek().Timestamp, nowTimestamp) > window
            )
            {
                samples.Dequeue();
            }
        }

        private readonly record struct Sample(long Timestamp, long CumulativeBytes);
    }
}
