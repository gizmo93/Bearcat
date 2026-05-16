using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleases.Dto;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseReadRepository
{
    Task<PagedResult<ReleaseDto>> SearchReleasesAsync(
        ReleaseSearchQuery query,
        CancellationToken cancellationToken = default
    );

    IReadOnlyList<ArchiverDto> GetArchiverFilterOptions();

    Task<ReleaseDto?> GetReleaseAsync(int releaseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArchiveConfigDto>> GetArchiveConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken
    );
}
