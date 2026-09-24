namespace Bearcat.Domain.Shared.Transfers;

public sealed record TransferFileProgressSnapshot(
    int FileId,
    string FileName,
    string SourceName,
    long TransferredBytes,
    long TotalBytes
)
{
    public double Percentage =>
        TotalBytes <= 0 ? 0 : Math.Round((double)TransferredBytes / TotalBytes * 100, 0);
}
