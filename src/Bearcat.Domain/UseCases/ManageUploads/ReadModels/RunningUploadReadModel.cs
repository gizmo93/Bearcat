using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads.ReadModels;

public record RunningUploadReadModel(
    int UploadId,
    int ReleaseId,
    string ReleaseName,
    string UploadConfigName,
    string? HosterRegistrationName,
    string? HosterAccountPoolName,
    UploadState UploadState,
    IReadOnlyList<RunningUploadReadModel.FileReadModel> Files
)
{
    public record FileReadModel(string FilePath, OnlineState? UploadedFileOnlineState);
}
