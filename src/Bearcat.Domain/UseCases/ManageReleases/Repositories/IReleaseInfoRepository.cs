using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseInfoRepository
{
    Task<IReadOnlyList<ActiveNfoDatabaseRegistrationDto>> GetActiveNfoDatabaseRegistrationsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Release>> GetReleasesWithoutInfoAsync(
        int count,
        CancellationToken cancellationToken = default
    );

    Task<bool> HasReleaseInfoAsync(
        int releaseId,
        string nfoDatabaseClassName,
        CancellationToken cancellationToken = default
    );

    void DetachPendingReleaseInfos(Release release);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
