using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseCollectionReleaseReadModel(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
    DateTime CreatedAt,
    int UploadConfigsCount,
    int OnlineUploadConfigsCount,
    DateTime? NotFullyOnlineSince,
    IReadOnlyList<ReleaseLatestUploadReadModel> LatestUploads
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
