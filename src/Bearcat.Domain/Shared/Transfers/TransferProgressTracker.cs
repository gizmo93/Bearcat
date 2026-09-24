using System.Collections.Concurrent;
using System.Diagnostics;

namespace Bearcat.Domain.Shared.Transfers;

public sealed class TransferProgressTracker : ITransferProgressTracker
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<TransferIdentifier, TransferState> states = new();

    public void StartTracking(
        TransferIdentifier identifier,
        IReadOnlyList<TransferFile> plannedFiles
    )
    {
        states[identifier] = new TransferState(Stopwatch.GetTimestamp(), plannedFiles);
    }

    public void BeginFile(
        TransferIdentifier identifier,
        int fileId,
        string fileName,
        string sourceName,
        long? totalBytes
    )
    {
        if (states.TryGetValue(identifier, out var state))
        {
            state.BeginFile(
                fileId: fileId,
                fileName: fileName,
                sourceName: sourceName,
                totalBytes: totalBytes
            );
        }
    }

    public void AddBytes(TransferIdentifier identifier, int fileId, long bytes)
    {
        if (states.TryGetValue(identifier, out var state))
        {
            state.AddBytes(
                fileId: fileId,
                bytes: bytes,
                nowTimestamp: Stopwatch.GetTimestamp(),
                sampleInterval: SampleInterval,
                window: SpeedWindow
            );
        }
    }

    public void StopTracking(TransferIdentifier identifier)
    {
        states.TryRemove(identifier, out _);
    }

    public TransferProgressSnapshot? Get(TransferIdentifier identifier)
    {
        if (!states.TryGetValue(identifier, out var state))
        {
            return null;
        }

        var bytesPerSecond = state.GetBytesPerSecond(Stopwatch.GetTimestamp(), SpeedWindow);
        var (files, isTotalKnown) = state.GetFiles();
        var transferredBytes = files.Sum(file => file.TransferredBytes);
        var totalBytes = isTotalKnown ? files.Sum(file => file.TotalBytes) : 0;

        return new TransferProgressSnapshot(
            Identifier: identifier,
            BytesPerSecond: bytesPerSecond,
            TransferredBytes: transferredBytes,
            TotalBytes: totalBytes,
            Files: files
        );
    }

    private sealed class TransferState
    {
        private readonly Lock gate = new();

        private readonly Queue<Sample> samples;

        private readonly Dictionary<int, FileProgress> progressPerFile;

        private long cumulativeBytes;

        private long lastSampleTimestamp;

        public TransferState(long startTimestamp, IReadOnlyList<TransferFile> plannedFiles)
        {
            samples = new Queue<Sample>([new Sample(startTimestamp, CumulativeBytes: 0)]);
            lastSampleTimestamp = startTimestamp;
            progressPerFile = plannedFiles.ToDictionary(
                file => file.FileId,
                file => new FileProgress(
                    FileName: file.FileName,
                    SourceName: file.SourceName,
                    TransferredBytes: file.IsAlreadyTransferred ? file.SizeBytes ?? 0 : 0,
                    TotalBytes: file.SizeBytes
                )
            );
        }

        public void BeginFile(int fileId, string fileName, string sourceName, long? totalBytes)
        {
            lock (gate)
            {
                var knownTotalBytes = progressPerFile.TryGetValue(fileId, out var current)
                    ? current.TotalBytes
                    : null;

                progressPerFile[fileId] = new FileProgress(
                    FileName: fileName,
                    SourceName: sourceName,
                    TransferredBytes: 0,
                    TotalBytes: totalBytes ?? knownTotalBytes
                );
            }
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

                if (progressPerFile.TryGetValue(fileId, out var fileProgress))
                {
                    progressPerFile[fileId] = fileProgress with
                    {
                        TransferredBytes = fileProgress.TransferredBytes + bytes,
                    };
                }

                if (Stopwatch.GetElapsedTime(lastSampleTimestamp, nowTimestamp) < sampleInterval)
                {
                    return;
                }

                lastSampleTimestamp = nowTimestamp;
                samples.Enqueue(new Sample(nowTimestamp, cumulativeBytes));
                RemoveOldSamples(nowTimestamp, window);
            }
        }

        public double GetBytesPerSecond(long nowTimestamp, TimeSpan window)
        {
            lock (gate)
            {
                RemoveOldSamples(nowTimestamp, window);

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

        public (IReadOnlyList<TransferFileProgressSnapshot> Files, bool IsTotalKnown) GetFiles()
        {
            lock (gate)
            {
                var files = progressPerFile
                    .Select(entry => new TransferFileProgressSnapshot(
                        FileId: entry.Key,
                        FileName: entry.Value.FileName,
                        SourceName: entry.Value.SourceName,
                        TransferredBytes: entry.Value.TotalBytes is { } totalBytes
                            ? Math.Min(entry.Value.TransferredBytes, totalBytes)
                            : entry.Value.TransferredBytes,
                        TotalBytes: entry.Value.TotalBytes ?? 0
                    ))
                    .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var isTotalKnown = progressPerFile.Values.All(file => file.TotalBytes is not null);

                return (files, isTotalKnown);
            }
        }

        private void RemoveOldSamples(long nowTimestamp, TimeSpan window)
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
            string SourceName,
            long TransferredBytes,
            long? TotalBytes
        );
    }
}
