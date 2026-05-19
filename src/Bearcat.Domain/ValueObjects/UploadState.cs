namespace Bearcat.Domain.ValueObjects;

public enum UploadState
{
    WaitingForArchive = 1,
    Pending = 2,
    Completed = 3,
    Failed = 4,
    Uploading = 5,
    CancellationRequested = 6,
    Canceled = 7,
}
