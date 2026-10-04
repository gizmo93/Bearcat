using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases.UploadHistory;

public static class UploadHistoryMarkerKindResolver
{
    public static UploadHistoryMarkerKind GetMarkerKind(
        UploadState uploadState,
        OnlineState onlineState
    )
    {
        return uploadState switch
        {
            UploadState.WaitingForArchive
            or UploadState.Pending
            or UploadState.Uploading
            or UploadState.CancellationRequested => UploadHistoryMarkerKind.InProgress,
            _ => onlineState switch
            {
                OnlineState.Online => UploadHistoryMarkerKind.Online,
                OnlineState.PartiallyOnline => UploadHistoryMarkerKind.PartiallyOnline,
                OnlineState.Offline => UploadHistoryMarkerKind.Offline,
                _ => UploadHistoryMarkerKind.Unknown,
            },
        };
    }
}
