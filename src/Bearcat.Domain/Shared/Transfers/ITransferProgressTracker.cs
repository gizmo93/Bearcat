namespace Bearcat.Domain.Shared.Transfers;

public interface ITransferProgressTracker
{
    void StartTracking(TransferKey key, IReadOnlyList<PlannedTransferFile> plannedFiles);

    void BeginFile(
        TransferKey key,
        int fileId,
        string fileName,
        string sourceName,
        long? totalBytes
    );

    void AddBytes(TransferKey key, int fileId, long bytes);

    void StopTracking(TransferKey key);

    TransferProgressSnapshot? Get(TransferKey key);
}
