namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

public interface IDownloadProgressTracker
{
    void StartTracking(int archiveId, IReadOnlyList<PlannedDownloadFile> plannedFiles);

    void BeginFile(
        int archiveId,
        int archiveFileId,
        string fileName,
        string hosterName,
        long? totalBytes
    );

    void AddBytes(int archiveId, int archiveFileId, long bytes);

    void StopTracking(int archiveId);

    DownloadProgressSnapshot? Get(int archiveId);
}
