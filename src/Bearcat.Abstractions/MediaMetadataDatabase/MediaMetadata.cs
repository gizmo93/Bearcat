namespace Bearcat.Abstractions.MediaMetadataDatabase;

public record MediaMetadata(
    string Title,
    string? Description,
    string? Genre,
    string? CoverUrl,
    string? DatabaseUrl
);
