namespace Bearcat.Domain.ValueObjects;

public enum RemoteSourceDownloadState
{
    Observing = 1,
    Pending = 2,
    Downloading = 3,
    Downloaded = 4,
    ReleaseCreated = 5,
    Failed = 6,
    Canceled = 7,
    Ignored = 8,
}
