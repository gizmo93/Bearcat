namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ReleaseForumPostUploadReadModel(
    string UploadConfigName,
    string HosterName,
    bool IsVideoUpload,
    string? ArchiveFormat,
    string? ArchivePassword,
    DateTime? UploadedAt,
    IReadOnlyList<string> Links,
    IReadOnlyList<string> EmbedLinks,
    IReadOnlyList<ReleaseForumPostLinkCrypterReadModel> LinkCrypters
);

public record ReleaseForumPostLinkCrypterReadModel(
    string Name,
    string? Password,
    string ContainerUrl,
    string? StatusImageId,
    DateTime CreatedAt
);
