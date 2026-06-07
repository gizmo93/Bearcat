using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;

public interface IReleaseCollectionReadRepository
{
    Task<PagedResult<ReleaseCollectionReadModel>> SearchAsync(
        ReleaseCollectionSearchQuery query,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseCollectionDetailReadModel?> GetDetailAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CollectionArchiveConfigOptionReadModel>> GetArchiveConfigOptionsAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );
}
