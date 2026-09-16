using System.Collections.Concurrent;
using System.Diagnostics;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

public sealed class DownloadProgressTracker : IDownloadProgressTracker
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<int, DownloadSpeedState> states = new();

    public void StartTracking(
        int archiveId,
        string hosterName,
        IReadOnlyList<PlannedDownloadFile> plannedFiles
    )
    {
        states[archiveId] = new DownloadSpeedState(
            Stopwatch.GetTimestamp(),
            hosterName,
            plannedFiles
        );
    }

    public void BeginFile(int archiveId, int archiveFileId, string fileName, long? totalBytes)
    {
        if (states.TryGetValue(archiveId, out var state))
        {
            state.BeginFile(archiveFileId, fileName, totalBytes);
        }
    }

    public void AddBytes(int archiveId, int archiveFileId, long bytes)
    {
        if (states.TryGetValue(archiveId, out var state))
        {
            state.AddBytes(
                archiveFileId,
                bytes,
                Stopwatch.GetTimestamp(),
                SampleInterval,
                SpeedWindow
            );
        }
    }

    public void StopTracking(int archiveId)
    {
        states.TryRemove(archiveId, out _);
    }

    public DownloadProgressSnapshot? Get(int archiveId)
    {
        if (!states.TryGetValue(archiveId, out var state))
        {
            return null;
        }

        var now = Stopwatch.GetTimestamp();
        var bytesPerSecond = state.GetBytesPerSecond(now, SpeedWindow);
        var files = state.GetFiles();
        var downloadedBytes = files.Sum(file => file.DownloadedBytes);
        var totalBytes = files.Any(file => file.TotalBytes <= 0)
            ? 0
            : files.Sum(file => file.TotalBytes);

        return new DownloadProgressSnapshot(
            ArchiveId: archiveId,
            HosterName: state.HosterName,
            BytesPerSecond: bytesPerSecond,
            DownloadedBytes: downloadedBytes,
            TotalBytes: totalBytes,
            Files: files
        );
    }

    private sealed class DownloadSpeedState
    {
        public string HosterName { get; }

        private readonly Lock gate = new();

        private readonly Queue<Sample> samples;

        private readonly Dictionary<int, FileProgress> progressPerFile;

        private long cumulativeBytes;

        private long lastSampleTimestamp;

        public DownloadSpeedState(
            long startTimestamp,
            string hosterName,
            IReadOnlyList<PlannedDownloadFile> plannedFiles
        )
        {
            HosterName = hosterName;
            samples = new Queue<Sample>([new Sample(startTimestamp, CumulativeBytes: 0)]);
            lastSampleTimestamp = startTimestamp;
            progressPerFile = plannedFiles.ToDictionary(
                file => file.ArchiveFileId,
                file => new FileProgress(
                    FileName: file.FileName,
                    DownloadedBytes: 0,
                    TotalBytes: file.SizeBytes ?? 0
                )
            );
        }

        public void BeginFile(int archiveFileId, string fileName, long? totalBytes)
        {
            lock (gate)
            {
                var knownTotalBytes = progressPerFile.TryGetValue(archiveFileId, out var current)
                    ? current.TotalBytes
                    : 0;

                progressPerFile[archiveFileId] = new FileProgress(
                    FileName: fileName,
                    DownloadedBytes: 0,
                    TotalBytes: totalBytes ?? knownTotalBytes
                );
            }
        }

        public void AddBytes(
            int archiveFileId,
            long bytes,
            long nowTimestamp,
            TimeSpan sampleInterval,
            TimeSpan window
        )
        {
            lock (gate)
            {
                cumulativeBytes += bytes;

                if (progressPerFile.TryGetValue(archiveFileId, out var fileProgress))
                {
                    progressPerFile[archiveFileId] = fileProgress with
                    {
                        DownloadedBytes = fileProgress.DownloadedBytes + bytes,
                    };
                }

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

        public IReadOnlyList<DownloadFileProgressSnapshot> GetFiles()
        {
            lock (gate)
            {
                return progressPerFile
                    .Select(entry => new DownloadFileProgressSnapshot(
                        ArchiveFileId: entry.Key,
                        FileName: entry.Value.FileName,
                        DownloadedBytes: entry.Value.TotalBytes > 0
                            ? Math.Min(entry.Value.DownloadedBytes, entry.Value.TotalBytes)
                            : entry.Value.DownloadedBytes,
                        TotalBytes: entry.Value.TotalBytes
                    ))
                    .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
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

        private readonly record struct FileProgress(
            string FileName,
            long DownloadedBytes,
            long TotalBytes
        );
    }
}
