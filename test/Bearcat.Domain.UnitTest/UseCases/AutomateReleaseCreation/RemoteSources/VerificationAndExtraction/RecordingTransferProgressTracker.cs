using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Shared.Transfers;
using Moq;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;

public sealed class RecordingTransferProgressTracker : ITransferProgressTracker
{
    private readonly TransferProgressTracker inner = new(Mock.Of<IProxyRoutingCache>());

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
        PlannedFilesPerIdentifier[identifier] = plannedFiles;
        inner.StartTracking(identifier, plannedFiles);
    }

    public void BeginFile(
        TransferIdentifier identifier,
        int fileId,
        string fileName,
        string sourceName,
        long? totalBytes
    )
    {
        inner.BeginFile(identifier, fileId, fileName, sourceName, totalBytes);
    }

    public void AddBytes(TransferIdentifier identifier, int fileId, long bytes)
    {
        inner.AddBytes(identifier, fileId, bytes);
        LastSnapshotPerIdentifier[identifier] = inner.Get(identifier)!;
    }

    public void StopTracking(TransferIdentifier identifier)
    {
        StoppedIdentifiers.Add(identifier);
        inner.StopTracking(identifier);
    }

    public TransferProgressSnapshot? Get(TransferIdentifier identifier)
    {
        return inner.Get(identifier);
    }

    public IReadOnlyList<int> GetTrackedIds(TransferType type)
    {
        return inner.GetTrackedIds(type);
    }
}
