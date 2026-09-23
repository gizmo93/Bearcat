namespace Bearcat.Domain.Shared.Transfers;

public sealed record TransferProgressSnapshot(
    TransferKey Key,
    double BytesPerSecond,
    long TransferredBytes,
    long TotalBytes,
    IReadOnlyList<TransferFileProgressSnapshot> Files
)
{
    public string SourceName =>
        string.Join(
            ", ",
            Files.Select(file => file.SourceName).Where(name => name.Length > 0).Distinct()
        );

    public double Percentage =>
        TotalBytes <= 0 ? 0 : Math.Round((double)TransferredBytes / TotalBytes * 100, 0);
}
