using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

/// <param name="Id">Id of the upload.</param>
/// <param name="ReleaseId">Id of the release.</param>
/// <param name="ReleaseName">Name of the release.</param>
/// <param name="UploadConfigId">Id of the upload config the upload belongs to.</param>
/// <param name="UploadConfigName">Name of the upload config the upload belongs to.</param>
/// <param name="HosterRegistrationId">Id of the hoster account that holds the files of the upload. Null while the upload config targets an account pool and no account of the pool is assigned yet.</param>
/// <param name="HosterRegistrationName">Name of the hoster account that holds the files of the upload, or null when no account is assigned yet.</param>
/// <param name="HosterAccountPoolId">Id of the account pool the upload config targets, or null when the upload config targets a single hoster account.</param>
/// <param name="HosterAccountPoolName">Name of the account pool the upload config targets, or null when the upload config targets a single hoster account.</param>
/// <param name="CreatedAt">Time the upload was created.</param>
/// <param name="UploadedAt">Time the upload was completed, or null.</param>
/// <param name="UploadState">State of the upload.</param>
/// <param name="OnlineState">Online state of the uploaded files.</param>
/// <param name="NotFullyOnlineSince">Time since which not all files are online, or null.</param>
/// <param name="FullyOfflineSince">Time since which all files are offline, or null.</param>
/// <param name="LinkCount">Number of uploaded files.</param>
/// <param name="ErrorMessages">Error messages of the upload.</param>
public record UploadWithReleaseResponse(
    int Id,
    int ReleaseId,
    string ReleaseName,
    int UploadConfigId,
    string UploadConfigName,
    int? HosterRegistrationId,
    string? HosterRegistrationName,
    int? HosterAccountPoolId,
    string? HosterAccountPoolName,
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
            HosterAccountPoolId: readModel.HosterAccountPoolId,
            HosterAccountPoolName: readModel.HosterAccountPoolName,
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
