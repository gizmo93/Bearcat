using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads.ReadModels;

public record UploadReadModel(
    int UploadId,
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
);
