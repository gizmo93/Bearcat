namespace Bearcat.Domain.Shared.ForumPostRendering;

/// <summary>
/// Loads the upload data of a single release that is needed to render forum posts. Lives in the
/// shared layer because both the release and the release collection render sources depend on it.
/// </summary>
public interface IReleaseForumPostUploadRepository
{
    Task<IReadOnlyList<ReleaseForumPostUploadReadModel>> GetForumPostUploadsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );
}
