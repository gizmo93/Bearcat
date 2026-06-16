namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

public interface IUploadProgressTracker
{
    void StartTracking(int uploadId);

    void AddBytes(int uploadId, long bytes);

    void StopTracking(int uploadId);

    UploadSpeedSnapshot? Get(int uploadId);
}
