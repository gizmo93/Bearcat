namespace Bearcat.Abstractions.Transfers;

public sealed class NullTransferProgress : ITransferProgress
{
    public static NullTransferProgress Instance { get; } = new();

    private NullTransferProgress() { }

    public void BeginFile(long? totalBytes) { }

    public void ReportBytesTransferred(long bytes) { }
}
