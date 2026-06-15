using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseReadModel(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
    ReleaseContentType ReleaseContentType,
    int ReleaseGroupId,
    string ReleaseGroupName,
    string ReleaseFolderPath,
    int ActiveUploadConfigsCount,
    int OnlineUploadConfigsCount
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
