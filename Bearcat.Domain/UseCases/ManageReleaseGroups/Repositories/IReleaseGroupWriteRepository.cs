using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;

public interface IReleaseGroupWriteRepository
{
    Task<ReleaseGroup> GetByIdAsync(int releaseGroupId, CancellationToken cancellationToken);

    Task<bool> HasAssignedReleasesAsync(int releaseGroupId, CancellationToken cancellationToken);

    void Add(ReleaseGroup releaseGroup);

    void Remove(ReleaseGroup releaseGroup);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
