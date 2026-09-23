using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;

public record RemoteSourceDownloadReadModel(
    int Id,
    string SourceName,
    string? AutomationName,
    string RemoteFolderPath,
    string FolderName,
    string LocalFolderPath,
    RemoteSourceDownloadState State,
    int FileCount,
    long TotalBytes,
    DateTime DiscoveredAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    int? ReleaseId,
    string? ReleaseName
)
{
    public bool CanCancel =>
        State
            is RemoteSourceDownloadState.Observing
                or RemoteSourceDownloadState.Pending
                or RemoteSourceDownloadState.Downloading;

    public bool CanRestart =>
        State is RemoteSourceDownloadState.Failed or RemoteSourceDownloadState.Canceled;

    public bool CanRetryReleaseCreation =>
        State is RemoteSourceDownloadState.Failed && CompletedAt is not null;

    public bool CanIgnore =>
        State
            is RemoteSourceDownloadState.Observing
                or RemoteSourceDownloadState.Pending
                or RemoteSourceDownloadState.Failed
                or RemoteSourceDownloadState.Canceled;
}
