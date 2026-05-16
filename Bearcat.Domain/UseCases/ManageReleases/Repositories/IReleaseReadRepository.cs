using Bearcat.Domain.UseCases.ManageReleases.Dto;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseReadRepository
{
    Task<IReadOnlyList<ReleaseDto>> GetReleasesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseDto?> GetReleaseAsync(int releaseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArchiveConfigDto>> GetArchiveConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken
    );
}
