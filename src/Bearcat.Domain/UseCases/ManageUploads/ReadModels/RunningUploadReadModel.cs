using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads.ReadModels;

public record RunningUploadReadModel(
    int UploadId,
    int ReleaseId,
    string ReleaseName,
    string UploadConfigName,
    string HosterRegistrationName,
    UploadState UploadState,
    IReadOnlyList<RunningUploadReadModel.ArchiveFileReadModel> ArchiveFiles
)
{
    public record ArchiveFileReadModel(string FullFileName, OnlineState? UploadedFileOnlineState);
}
