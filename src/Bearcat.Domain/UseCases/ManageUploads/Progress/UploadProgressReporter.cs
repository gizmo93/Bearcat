using Bearcat.Abstractions.Hoster;

namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

public sealed class UploadProgressReporter(IUploadProgressTracker tracker, int uploadId)
    : IUploadProgress
{
    public void ReportBytesTransferred(long bytes)
    {
        tracker.AddBytes(uploadId, bytes);
    }
}
