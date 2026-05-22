using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseOverviewUploadReadModel(
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
    IReadOnlyList<ReleaseOverviewLinkCrypterLinkReadModel> LinkCrypterLinks
);
