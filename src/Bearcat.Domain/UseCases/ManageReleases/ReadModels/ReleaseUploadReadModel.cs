using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseUploadReadModel(
    int UploadId,
    string UploadConfigName,
    string HosterRegistrationName,
    DateTime CreatedAt,
    DateTime? UploadedAt,
    UploadState UploadState,
    OnlineState OnlineState,
    int LinkCount,
    int ContainerCount,
    bool CanCreateReupload
);
