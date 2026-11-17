namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ReleaseForumPostServiceLinks(
    IReadOnlyList<ReleaseForumPostUploadReadModel> Uploads,
    IReadOnlyList<ReleaseForumPostLinkCrypterReadModel> AllHostersLinkCrypters,
    IReadOnlyList<ForumPostImageLinkReadModel> ImageLinks
);
