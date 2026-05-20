using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseOverviewUploadDto(
    int UploadConfigId,
    string UploadConfigName,
    string HosterRegistrationName,
    int? UploadId,
    DateTime? CreatedAt,
    DateTime? UploadedAt,
    UploadState? UploadState,
    OnlineState? OnlineState,
    int LinkCount,
    string? ArchivePassword,
    IReadOnlyList<ReleaseOverviewLinkCrypterLinkDto> LinkCrypterLinks
);
