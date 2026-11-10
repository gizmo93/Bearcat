namespace Bearcat.Domain.Shared.ForumPostRendering;

public interface IForumPostImageLinkRepository
{
    Task<
        IReadOnlyDictionary<int, IReadOnlyList<ForumPostImageLinkReadModel>>
    > GetReleaseImageLinksByReleaseIdAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ForumPostImageLinkReadModel>> GetCollectionImageLinksAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );
}
