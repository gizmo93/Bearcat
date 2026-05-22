using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseInfoRepository
{
    Task<
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel>
    > GetActiveNfoDatabaseRegistrationsAsync(CancellationToken cancellationToken = default);

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
