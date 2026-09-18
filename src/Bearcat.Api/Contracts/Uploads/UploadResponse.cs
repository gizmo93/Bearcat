using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

public record UploadResponse(
    int Id,
    string UploadConfigName,
    string HosterRegistrationName,
    DateTime CreatedAt,
    DateTime? UploadedAt,
    UploadState UploadState,
    OnlineState OnlineState,
    DateTime? NotFullyOnlineSince,
    int LinkCount,
    int ContainerCount,
    bool CanCreateReupload,
    IReadOnlyList<string> ErrorMessages
)
{
    public static UploadResponse FromReadModel(ReleaseUploadReadModel readModel)
    {
        return new UploadResponse(
            Id: readModel.UploadId,
            UploadConfigName: readModel.UploadConfigName,
            HosterRegistrationName: readModel.HosterRegistrationName,
            CreatedAt: readModel.CreatedAt,
            UploadedAt: readModel.UploadedAt,
            UploadState: readModel.UploadState,
            OnlineState: readModel.OnlineState,
            NotFullyOnlineSince: readModel.NotFullyOnlineSince,
            LinkCount: readModel.LinkCount,
            ContainerCount: readModel.ContainerCount,
            CanCreateReupload: readModel.CanCreateReupload,
            ErrorMessages: readModel.ErrorMessages
        );
    }
}
