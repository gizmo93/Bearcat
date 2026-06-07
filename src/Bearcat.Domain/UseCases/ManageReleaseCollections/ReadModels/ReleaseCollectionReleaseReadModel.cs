using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseCollectionReleaseReadModel(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
    DateTime CreatedAt,
    int ActiveUploadConfigsCount,
    int OnlineUploadConfigsCount,
    int? LatestUploadId,
    string? LatestUploadConfigName
)
{
    public OnlineState? OnlineState
    {
        get
        {
            if (ActiveUploadConfigsCount == 0)
            {
                return null;
            }

            if (ActiveUploadConfigsCount == OnlineUploadConfigsCount)
            {
                return ValueObjects.OnlineState.Online;
            }

            return OnlineUploadConfigsCount > 0
                ? ValueObjects.OnlineState.PartiallyOnline
                : ValueObjects.OnlineState.Offline;
        }
    }
}
