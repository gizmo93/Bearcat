namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ForumPostScreenshotImageLinkReadModel(
    int Index,
    TimeSpan Timestamp,
    IReadOnlyList<ForumPostImageLinkUrlReadModel> Urls
);
