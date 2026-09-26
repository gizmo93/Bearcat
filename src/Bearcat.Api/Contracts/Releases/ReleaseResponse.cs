using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Releases;

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
    bool ExcludeFromAutoCleanup,
    DateTime? UploadsPostedAt
)
{
    public static ReleaseResponse FromReadModel(ReleaseReadModel readModel)
    {
        return new ReleaseResponse(
            Id: readModel.ReleaseId,
            Name: readModel.Name,
            ReleaseType: readModel.ReleaseType,
            ReleaseContentType: readModel.ReleaseContentType,
            PrimaryLanguageCode: readModel.PrimaryLanguageCode,
            ReleaseGroupId: readModel.ReleaseGroupId,
            ReleaseGroupName: readModel.ReleaseGroupName,
            ReleaseFolderPath: readModel.ReleaseFolderPath,
            OnlineState: readModel.OnlineState,
            ActiveUploadConfigsCount: readModel.ActiveUploadConfigsCount,
            OnlineUploadConfigsCount: readModel.OnlineUploadConfigsCount,
            ExcludeFromAutoCleanup: readModel.ExcludeFromAutoCleanup,
            UploadsPostedAt: readModel.UploadsPostedAt
        );
    }
}
