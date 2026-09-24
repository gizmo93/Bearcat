using Bearcat.Abstractions.Transfers;

namespace Bearcat.Domain.Shared.Transfers;

public sealed class TransferProgressReporter(
    ITransferProgressTracker tracker,
    TransferIdentifier identifier,
    int fileId,
    string fileName,
    string sourceName
) : ITransferProgress
{
    public void BeginFile(long? totalBytes)
    {
        tracker.BeginFile(identifier, fileId, fileName, sourceName, totalBytes);
    }

    public void ReportBytesTransferred(long bytes)
    {
        tracker.AddBytes(identifier, fileId, bytes);
    }
}
