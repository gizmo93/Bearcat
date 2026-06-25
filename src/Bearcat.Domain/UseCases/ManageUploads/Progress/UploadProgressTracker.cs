using System.Collections.Concurrent;
using System.Diagnostics;

namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

public sealed class UploadProgressTracker : IUploadProgressTracker
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<int, UploadSpeedState> states = new();

    public void StartTracking(int uploadId, long totalBytes, long alreadyUploadedBytes)
    {
        states[uploadId] = new UploadSpeedState(
            startTimestamp: Stopwatch.GetTimestamp(),
            totalBytes: totalBytes,
            baselineBytes: alreadyUploadedBytes
        );
    }

    public void AddBytes(int uploadId, int fileId, long bytes)
    {
        if (states.TryGetValue(uploadId, out var state))
        {
            state.AddBytes(fileId, bytes, Stopwatch.GetTimestamp(), SampleInterval, SpeedWindow);
        }
    }

    public void ResetFile(int uploadId, int fileId)
    {
        if (states.TryGetValue(uploadId, out var state))
        {
            state.ResetFile(fileId);
        }
    }

    public void StopTracking(int uploadId)
    {
        states.TryRemove(uploadId, out _);
    }

    public UploadProgressSnapshot? Get(int uploadId)
    {
        if (!states.TryGetValue(uploadId, out var state))
        {
            return null;
        }

        var now = Stopwatch.GetTimestamp();
        var bytesPerSecond = state.GetBytesPerSecond(now, SpeedWindow);
        var (uploadedBytes, totalBytes) = state.GetProgress();

        return new UploadProgressSnapshot(
            UploadId: uploadId,
            BytesPerSecond: bytesPerSecond,
            UploadedBytes: uploadedBytes,
            TotalBytes: totalBytes
        );
    }

    private sealed class UploadSpeedState
    {
        private readonly Lock gate = new();

        private readonly Queue<Sample> samples = new();

        private readonly Dictionary<int, long> bytesPerFile = new();

        private readonly long totalBytes;

        private readonly long baselineBytes;

        private long cumulativeBytes;

        private long lastSampleTimestamp;

        public UploadSpeedState(long startTimestamp, long totalBytes, long baselineBytes)
        {
            this.totalBytes = totalBytes;
            this.baselineBytes = baselineBytes;
            lastSampleTimestamp = startTimestamp;
            samples.Enqueue(new Sample(startTimestamp, CumulativeBytes: 0));
        }

        public void AddBytes(
            int fileId,
            long bytes,
            long nowTimestamp,
            TimeSpan sampleInterval,
            TimeSpan window
        )
        {
            lock (gate)
            {
                cumulativeBytes += bytes;
                bytesPerFile[fileId] = bytesPerFile.GetValueOrDefault(fileId) + bytes;

                if (Stopwatch.GetElapsedTime(lastSampleTimestamp, nowTimestamp) < sampleInterval)
                {
                    return;
                }

                lastSampleTimestamp = nowTimestamp;
                samples.Enqueue(new Sample(nowTimestamp, cumulativeBytes));
                TrimOldSamples(nowTimestamp, window);
            }
        }

        public void ResetFile(int fileId)
        {
            lock (gate)
            {
                bytesPerFile[fileId] = 0;
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

        public (long UploadedBytes, long TotalBytes) GetProgress()
        {
            lock (gate)
            {
                var uploadedBytes = baselineBytes + bytesPerFile.Values.Sum();

                if (totalBytes > 0 && uploadedBytes > totalBytes)
                {
                    uploadedBytes = totalBytes;
                }

                return (uploadedBytes, totalBytes);
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
