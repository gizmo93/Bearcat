using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;

public interface IReleaseCollectionInfoRepository
{
    Task<
        IReadOnlyList<ActiveSeriesDatabaseRegistrationReadModel>
    > GetActiveSeriesDatabaseRegistrationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReleaseCollection>> GetCollectionsWithoutMetadataAsync(
        int count,
        DateTime lastCheckedThreshold,
        HashSet<int> excludedCollectionIds,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseCollection?> GetByIdForResolutionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );

    void DetachPendingMetadata(ReleaseCollection collection);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
