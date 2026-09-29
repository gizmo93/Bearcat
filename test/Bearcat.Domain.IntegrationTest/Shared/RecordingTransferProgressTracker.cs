using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Shared.Transfers;
using Moq;

namespace Bearcat.Domain.IntegrationTest.Shared;

public sealed class RecordingTransferProgressTracker : ITransferProgressTracker
{
    private readonly TransferProgressTracker inner = new(Mock.Of<IProxyRoutingCache>());

    private readonly Lock recordingLock = new();

    public Dictionary<
        TransferIdentifier,
        IReadOnlyList<TransferFile>
    > PlannedFilesPerIdentifier { get; } = new();

    public Dictionary<
        TransferIdentifier,
        TransferProgressSnapshot
    > LastSnapshotPerIdentifier { get; } = new();

    public List<TransferIdentifier> StoppedIdentifiers { get; } = [];

    public void StartTracking(
        TransferIdentifier identifier,
        IReadOnlyList<TransferFile> plannedFiles
    )
    {
        lock (recordingLock)
        {
            PlannedFilesPerIdentifier[identifier] = plannedFiles;
            inner.StartTracking(identifier, plannedFiles);
        }
    }

    public void BeginFile(
        TransferIdentifier identifier,
        int fileId,
        string fileName,
        string sourceName,
        long? totalBytes
    )
    {
        lock (recordingLock)
        {
            inner.BeginFile(identifier, fileId, fileName, sourceName, totalBytes);
            LastSnapshotPerIdentifier[identifier] = inner.Get(identifier)!;
        }
    }

    public void AddBytes(TransferIdentifier identifier, int fileId, long bytes)
    {
        lock (recordingLock)
        {
            inner.AddBytes(identifier, fileId, bytes);
            LastSnapshotPerIdentifier[identifier] = inner.Get(identifier)!;
        }
    }

    public void StopTracking(TransferIdentifier identifier)
    {
        lock (recordingLock)
        {
            StoppedIdentifiers.Add(identifier);
            inner.StopTracking(identifier);
        }
    }

    public TransferProgressSnapshot? Get(TransferIdentifier identifier)
    {
        return inner.Get(identifier);
    }
}
