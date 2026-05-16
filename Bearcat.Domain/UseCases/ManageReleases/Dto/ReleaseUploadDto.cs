using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseUploadDto(
    int UploadId,
    string UploadConfigName,
    string HosterRegistrationName,
    DateTime CreatedAt,
    DateTime? UploadedAt,
    UploadState UploadState,
    OnlineState OnlineState,
    int LinkCount
);
