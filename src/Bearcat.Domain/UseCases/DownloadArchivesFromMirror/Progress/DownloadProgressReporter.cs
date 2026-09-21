using Bearcat.Abstractions.Hoster;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

public sealed class DownloadProgressReporter(
    IDownloadProgressTracker tracker,
    int archiveId,
    int archiveFileId,
    string fileName,
    string hosterName
) : IDownloadProgress
{
    public void BeginFile(long? totalBytes)
    {
        tracker.BeginFile(archiveId, archiveFileId, fileName, hosterName, totalBytes);
    }

    public void ReportBytesTransferred(long bytes)
    {
        tracker.AddBytes(archiveId, archiveFileId, bytes);
    }
}
