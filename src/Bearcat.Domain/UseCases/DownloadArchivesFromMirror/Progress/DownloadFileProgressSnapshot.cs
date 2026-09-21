namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

public sealed record DownloadFileProgressSnapshot(
    int ArchiveFileId,
    string FileName,
    string HosterName,
    long DownloadedBytes,
    long TotalBytes
)
{
    public double Percentage =>
        TotalBytes <= 0 ? 0 : Math.Round((double)DownloadedBytes / TotalBytes * 100, 0);
}
