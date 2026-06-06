namespace Bearcat.Abstractions.ImageHoster.Dto;

public record ImageToUploadDto(
    string Source,
    ImageUploadSource SourceType,
    string? Name = null,
    int? ExpirationSeconds = null
);
