using Bearcat.Abstractions.ImageHoster.Results;

namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ForumPostImageLinkReadModel(
    string ImageUploadConfigName,
    IReadOnlyList<ForumPostImageLinkUrlReadModel> Urls
);

public record ForumPostImageLinkUrlReadModel(ImageSize Size, string Url);
