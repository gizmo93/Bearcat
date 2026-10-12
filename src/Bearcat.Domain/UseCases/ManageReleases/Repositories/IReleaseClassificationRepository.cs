using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseClassificationRepository
{
    Task<Release?> GetReleaseForClassificationAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Release>> GetReleasesNeedingClassificationAsync(
        int count,
        int parserVersion,
        int afterReleaseId,
        CancellationToken cancellationToken = default
    );

    void ClearChangeTracker();

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
