namespace Bearcat.Abstractions.Transfers;

public interface ITransferProgress
{
    void BeginFile(long? totalBytes);

    void ReportBytesTransferred(long bytes);
}
