using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseDto(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
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
