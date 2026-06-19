namespace Bearcat.ImageHosters.DirectUpload.Api;

public record UploadResponse(
    string ImageId,
    string DirectUrl,
    string? ThumbnailUrl,
    string? DeleteUrl
);
