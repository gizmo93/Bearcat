namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

public interface IDownloadProgressTracker
{
    void StartTracking(
        int archiveId,
        string hosterName,
        IReadOnlyList<PlannedDownloadFile> plannedFiles
    );

    void BeginFile(int archiveId, int archiveFileId, string fileName, long? totalBytes);

    void AddBytes(int archiveId, int archiveFileId, long bytes);

    void StopTracking(int archiveId);

    DownloadProgressSnapshot? Get(int archiveId);
}
