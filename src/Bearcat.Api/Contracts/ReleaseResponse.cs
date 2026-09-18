using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts;

public record ReleaseResponse(
    int Id,
    string Name,
    ReleaseType ReleaseType,
    ReleaseContentType ReleaseContentType,
    string? PrimaryLanguageCode,
    int ReleaseGroupId,
    string ReleaseGroupName,
    string? ReleaseFolderPath,
    OnlineState? OnlineState,
    int ActiveUploadConfigsCount,
    int OnlineUploadConfigsCount,
    bool ExcludeFromAutoCleanup
)
{
    public static ReleaseResponse FromReadModel(ReleaseReadModel readModel)
    {
        return new ReleaseResponse(
            readModel.ReleaseId,
            readModel.Name,
            readModel.ReleaseType,
            readModel.ReleaseContentType,
            readModel.PrimaryLanguageCode,
            readModel.ReleaseGroupId,
            readModel.ReleaseGroupName,
            readModel.ReleaseFolderPath,
            readModel.OnlineState,
            readModel.ActiveUploadConfigsCount,
            readModel.OnlineUploadConfigsCount,
            readModel.ExcludeFromAutoCleanup
        );
    }
}
