namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

public interface IUploadProgressTracker
{
    void StartTracking(int uploadId, long totalBytes, long alreadyUploadedBytes);

    void AddBytes(int uploadId, long bytes);

    void StopTracking(int uploadId);

    UploadProgressSnapshot? Get(int uploadId);
}
