using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;

public interface IReleaseCollectionInfoRepository
{
    Task<IReadOnlyList<ReleaseCollection>> GetCollectionsWithoutMetadataAsync(
        int count,
        DateTime lastCheckedThreshold,
        DateTime? afterCreatedAt,
        int afterCollectionId,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseCollection?> GetByIdForResolutionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseCollection> GetByIdForMetadataCorrectionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );

    void Remove(ReleaseCollectionMetadata metadata);

    void DetachPendingMetadata(ReleaseCollection collection);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
