using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads.ReadModels;

public record UploadReadModel(
    int UploadId,
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
    IReadOnlyList<string> ErrorMessages,
    int? ArchiveId
);
