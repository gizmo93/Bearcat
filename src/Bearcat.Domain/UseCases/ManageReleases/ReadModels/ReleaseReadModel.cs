using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseReadModel(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
    ReleaseContentType ReleaseContentType,
    string? PrimaryLanguageCode,
    int ReleaseGroupId,
    string ReleaseGroupName,
    string? ReleaseFolderPath,
    int UploadConfigsCount,
    int OnlineUploadConfigsCount,
    bool ExcludeFromAutoCleanup,
    DateTime? UploadsPostedAt
)
{
    public OnlineState? OnlineState
    {
        get
        {
            if (UploadConfigsCount == 0)
            {
                return null;
            }

            if (UploadConfigsCount == OnlineUploadConfigsCount)
            {
                return ValueObjects.OnlineState.Online;
            }

            return OnlineUploadConfigsCount > 0
                ? ValueObjects.OnlineState.PartiallyOnline
                : ValueObjects.OnlineState.Offline;
        }
    }
}
