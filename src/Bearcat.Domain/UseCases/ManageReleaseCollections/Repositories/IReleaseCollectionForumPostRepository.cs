using Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;

public interface IReleaseCollectionForumPostRepository
{
    Task<CollectionForumPostReadModel?> GetAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );
}
