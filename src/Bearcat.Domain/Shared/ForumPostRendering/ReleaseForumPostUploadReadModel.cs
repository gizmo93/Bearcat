namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ReleaseForumPostUploadReadModel(
    string UploadConfigName,
    string HosterClassName,
    string HosterName,
    string HosterRegistrationName,
    bool IsVideoUpload,
    string? ArchiveFormat,
    string? ArchivePassword,
    DateTime? UploadedAt,
    IReadOnlyList<string> Links,
    IReadOnlyList<string> EmbedLinks,
    IReadOnlyList<ReleaseForumPostLinkCrypterReadModel> LinkCrypters
);

public record ReleaseForumPostLinkCrypterReadModel(
    string LinkCrypterClassName,
    string Name,
    string? Password,
    string ContainerUrl,
    string? StatusImageId,
    DateTime CreatedAt
);
