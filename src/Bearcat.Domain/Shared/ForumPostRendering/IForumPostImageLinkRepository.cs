namespace Bearcat.Domain.Shared.ForumPostRendering;

public interface IForumPostImageLinkRepository
{
    Task<IReadOnlyList<ForumPostImageLinkReadModel>> GetReleaseImageLinksAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ForumPostImageLinkReadModel>> GetCollectionImageLinksAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );
}
