namespace Bearcat.Abstractions.MediaMetadataDatabase;

public record MediaMetadataLookup(
    MediaKind MediaKind,
    string? ImdbId,
    string? Title,
    int? Year,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? LanguageCode
);
