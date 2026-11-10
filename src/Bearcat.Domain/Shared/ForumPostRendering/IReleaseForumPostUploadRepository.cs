namespace Bearcat.Domain.Shared.ForumPostRendering;

/// <summary>
/// Loads the upload data of releases that is needed to render forum posts, keyed by release id.
/// Lives in the shared layer because both the release and the release collection render sources
/// depend on it.
/// </summary>
public interface IReleaseForumPostUploadRepository
{
    Task<
        IReadOnlyDictionary<int, IReadOnlyList<ReleaseForumPostUploadReadModel>>
    > GetForumPostUploadsByReleaseIdAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    );

    Task<
        IReadOnlyDictionary<int, IReadOnlyList<ReleaseForumPostLinkCrypterReadModel>>
    > GetForumPostAllHostersLinkCryptersByReleaseIdAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    );
}
