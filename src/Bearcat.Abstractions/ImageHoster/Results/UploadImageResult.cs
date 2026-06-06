using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.Abstractions.ImageHoster.Results;

public record UploadImageResult(
    bool IsSuccess,
    ImageToUploadDto Image,
    IReadOnlyList<ImageUrl> ImageUrls,
    IReadOnlyList<string> ErrorMessages,
    string? DeleteUrl = null,
    string? ExternalId = null
);
