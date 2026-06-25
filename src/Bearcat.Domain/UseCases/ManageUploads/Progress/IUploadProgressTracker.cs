namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

public interface IUploadProgressTracker
{
    void StartTracking(int uploadId, long totalBytes, long alreadyUploadedBytes);

    void AddBytes(int uploadId, int fileId, long bytes);

    void ResetFile(int uploadId, int fileId);

    void StopTracking(int uploadId);

    UploadProgressSnapshot? Get(int uploadId);
}
