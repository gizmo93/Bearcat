using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseSearchResultReadModel(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
    ReleaseContentType ReleaseContentType,
    string? PrimaryLanguageCode,
    int ReleaseGroupId,
    string ReleaseGroupName,
    string? ReleaseFolderPath,
    DateTime CreatedAt,
    DateTime? UploadsPostedAt,
    string? MetadataTitle,
    string? CoverUrl,
    int? Year,
    ReleaseResolution? Resolution,
    ReleaseSource? Source,
    IReadOnlyList<ReleaseSearchResultUploadConfigReadModel> UploadConfigs,
    bool IsReadyForPostQueue
)
{
    public OnlineState? OnlineState
    {
        get
        {
            if (UploadConfigs.Count == 0)
            {
                return null;
            }

            var onlineUploadConfigsCount = UploadConfigs.Count(uploadConfig =>
                uploadConfig.IsOnline
            );

            if (onlineUploadConfigsCount == UploadConfigs.Count)
            {
                return ValueObjects.OnlineState.Online;
            }

            return onlineUploadConfigsCount > 0
                ? ValueObjects.OnlineState.PartiallyOnline
                : ValueObjects.OnlineState.Offline;
        }
    }
}
