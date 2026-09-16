namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

public sealed record DownloadProgressSnapshot(
    int ArchiveId,
    string HosterName,
    double BytesPerSecond,
    long DownloadedBytes,
    long TotalBytes,
    IReadOnlyList<DownloadFileProgressSnapshot> Files
)
{
    public double Percentage =>
        TotalBytes <= 0 ? 0 : Math.Round((double)DownloadedBytes / TotalBytes * 100, 0);
}
