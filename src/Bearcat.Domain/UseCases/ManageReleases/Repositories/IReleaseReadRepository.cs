using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Shared;
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

    Task<IReadOnlyList<ReleaseOverviewUploadReadModel>> GetReleaseOverviewAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseInfoReadModel>> GetReleaseInfosAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseNfoReadModel?> GetReleaseNfoAsync(
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
}
