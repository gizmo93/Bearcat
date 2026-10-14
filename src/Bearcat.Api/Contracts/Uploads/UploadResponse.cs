using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

/// <param name="Id">Id of the upload.</param>
/// <param name="UploadConfigName">Name of the upload config the upload belongs to.</param>
/// <param name="HosterRegistrationName">Name of the hoster account that holds the files of the upload. Null while the upload config targets an account pool and no account of the pool is assigned yet.</param>
/// <param name="HosterAccountPoolName">Name of the account pool the upload config targets, or null when the upload config targets a single hoster account.</param>
/// <param name="CreatedAt">Time the upload was created.</param>
/// <param name="UploadedAt">Time the upload was completed, or null.</param>
/// <param name="UploadState">State of the upload.</param>
/// <param name="OnlineState">Online state of the uploaded files.</param>
/// <param name="NotFullyOnlineSince">Time since which not all files are online, or null.</param>
/// <param name="LinkCount">Number of uploaded files.</param>
/// <param name="ContainerCount">Number of link crypter containers that contain the upload.</param>
/// <param name="CanCreateReupload">True when a manual reupload can be created for this upload.</param>
/// <param name="ErrorMessages">Error messages of the upload.</param>
public record UploadResponse(
    int Id,
    string UploadConfigName,
    string? HosterRegistrationName,
    string? HosterAccountPoolName,
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
            HosterAccountPoolName: readModel.HosterAccountPoolName,
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
