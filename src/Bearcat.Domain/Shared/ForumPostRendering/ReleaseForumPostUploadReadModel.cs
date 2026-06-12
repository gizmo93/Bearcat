namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ReleaseForumPostUploadReadModel(
    string UploadConfigName,
    string HosterName,
    string ArchiveFormat,
    string? ArchivePassword,
    DateTime? UploadedAt,
    IReadOnlyList<string> Links,
    IReadOnlyList<ReleaseForumPostLinkCrypterReadModel> LinkCrypters
);

public record ReleaseForumPostLinkCrypterReadModel(
    string Name,
    string? Password,
    string ContainerUrl,
    DateTime CreatedAt
);
