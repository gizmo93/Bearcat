using Bearcat.Abstractions.Hoster;

namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

public sealed class UploadProgressReporter(IUploadProgressTracker tracker, int uploadId, int fileId)
    : IUploadProgress
{
    public void BeginFile()
    {
        tracker.ResetFile(uploadId, fileId);
    }

    public void ReportBytesTransferred(long bytes)
    {
        tracker.AddBytes(uploadId, fileId, bytes);
    }
}
