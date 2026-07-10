namespace Bearcat.Abstractions.MediaMetadataDatabase;

public record MediaMetadataDatabaseDto(
    string Name,
    string ClassName,
    IReadOnlyList<string> ConfigurationKeys
);
