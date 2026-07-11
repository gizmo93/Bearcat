using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseReadRepository
{
    Task<PagedResult<ReleaseReadModel>> SearchReleasesAsync(
        ReleaseSearchQuery query,
        CancellationToken cancellationToken = default
    );

    IReadOnlyList<ArchiverDto> GetArchiverFilterOptions();

    Task<ReleaseReadModel?> GetReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<string>> GetUnmanagedArchiveFolderPathsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseOverviewUploadReadModel>> GetReleaseOverviewAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleasePostQueueItemReadModel>> GetPostQueueAsync(
        CancellationToken cancellationToken = default
    );

    Task<int> CountPostQueueAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReleaseQualityIssueQueueItemReadModel>> GetQualityIssuesQueueAsync(
        CancellationToken cancellationToken = default
    );

    Task<int> CountQualityIssuesQueueAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReleaseOverviewImageUploadReadModel>> GetReleaseOverviewImageUploadsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseInfoReadModel?> GetReleaseInfoAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseMetadataReadModel?> GetReleaseMetadataAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseNfoReadModel?> GetReleaseNfoAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseExternalIdentifierReadModel>> GetReleaseExternalIdentifiersAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseMediaFileReadModel>> GetMediaFilesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ArchiveConfigReadModel>> GetArchiveConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken
    );

    Task<PagedResult<ReleaseUploadReadModel>> SearchUploadsAsync(
        ReleaseUploadSearchQuery query,
        CancellationToken cancellationToken = default
    );

    Task<PagedResult<ReleaseUploadLinkReadModel>> SearchUploadLinksAsync(
        ReleaseUploadLinkSearchQuery query,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<string>> GetUploadLinksAsync(
        int releaseId,
        int uploadId,
        OnlineState? onlineState = null,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseUploadContainerLinkReadModel>> GetUploadContainerLinksAsync(
        int releaseId,
        int uploadId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseImageUploadReadModel>> GetImageUploadsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseImageUploadUrlReadModel>> GetImageUploadUrlsAsync(
        int releaseId,
        int imageUploadId,
        CancellationToken cancellationToken = default
    );
}
