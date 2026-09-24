namespace Bearcat.Domain.Shared.Transfers;

public interface ITransferProgressTracker
{
    void StartTracking(
        TransferIdentifier identifier,
        IReadOnlyList<PlannedTransferFile> plannedFiles
    );

    void BeginFile(
        TransferIdentifier identifier,
        int fileId,
        string fileName,
        string sourceName,
        long? totalBytes
    );

    void AddBytes(TransferIdentifier identifier, int fileId, long bytes);

    void StopTracking(TransferIdentifier identifier);

    TransferProgressSnapshot? Get(TransferIdentifier identifier);
}
