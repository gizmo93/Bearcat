using Bearcat.Abstractions.Transfers;

namespace Bearcat.RemoteSources.IntegrationTest;

public sealed class RecordingTransferProgress(Action? onBytesTransferred = null) : ITransferProgress
{
    public List<long?> BeganFiles { get; } = [];

    public List<long> TransferredBytes { get; } = [];

    public void BeginFile(long? totalBytes)
    {
        BeganFiles.Add(totalBytes);
    }

    public void ReportBytesTransferred(long bytes)
    {
        TransferredBytes.Add(bytes);
        onBytesTransferred?.Invoke();
    }
}
