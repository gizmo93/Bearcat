using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;

public interface IReleaseCollectionWriteRepository
{
    Task<ReleaseCollection?> GetByReleaseGroupAndKeyAsync(
        int releaseGroupId,
        string key,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseCollection> GetByIdAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );

    void Add(ReleaseCollection releaseCollection);

    void Remove(ReleaseCollection releaseCollection);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
