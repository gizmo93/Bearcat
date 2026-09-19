namespace Bearcat.Abstractions.MediaMetadataDatabase;

public record MediaMetadataLookup(
    MediaKind MediaKind,
    string? ImdbId,
    string? SteamAppId,
    string? Title,
    int? Year,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? LanguageCode
);
