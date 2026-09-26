using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

public record UploadWithReleaseResponse(
    int Id,
    int ReleaseId,
    string ReleaseName,
    int UploadConfigId,
    string UploadConfigName,
    int HosterRegistrationId,
    string HosterRegistrationName,
    DateTime CreatedAt,
    DateTime? UploadedAt,
    UploadState UploadState,
    OnlineState OnlineState,
    DateTime? NotFullyOnlineSince,
    DateTime? FullyOfflineSince,
    int LinkCount,
    IReadOnlyList<string> ErrorMessages
)
{
    public static UploadWithReleaseResponse FromReadModel(UploadReadModel readModel)
    {
        return new UploadWithReleaseResponse(
            Id: readModel.UploadId,
            ReleaseId: readModel.ReleaseId,
            ReleaseName: readModel.ReleaseName,
            UploadConfigId: readModel.UploadConfigId,
            UploadConfigName: readModel.UploadConfigName,
            HosterRegistrationId: readModel.HosterRegistrationId,
            HosterRegistrationName: readModel.HosterRegistrationName,
            CreatedAt: readModel.CreatedAt,
            UploadedAt: readModel.UploadedAt,
            UploadState: readModel.UploadState,
            OnlineState: readModel.OnlineState,
            NotFullyOnlineSince: readModel.NotFullyOnlineSince,
            FullyOfflineSince: readModel.FullyOfflineSince,
            LinkCount: readModel.LinkCount,
            ErrorMessages: readModel.ErrorMessages
        );
    }
}
