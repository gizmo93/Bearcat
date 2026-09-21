namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

public sealed record DownloadProgressSnapshot(
    int ArchiveId,
    double BytesPerSecond,
    long DownloadedBytes,
    long TotalBytes,
    IReadOnlyList<DownloadFileProgressSnapshot> Files
)
{
    public string HosterName =>
        string.Join(
            ", ",
            Files.Select(file => file.HosterName).Where(name => name.Length > 0).Distinct()
        );

    public double Percentage =>
        TotalBytes <= 0 ? 0 : Math.Round((double)DownloadedBytes / TotalBytes * 100, 0);
}
