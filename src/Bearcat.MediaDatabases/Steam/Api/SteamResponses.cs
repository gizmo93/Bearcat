using System.Text.Json.Serialization;

namespace Bearcat.MediaDatabases.Steam.Api;

public record SteamAppDetailsResponse(bool Success, SteamAppDataResponse? Data);

public record SteamAppDataResponse(
    string? Type,
    string? Name,
    [property: JsonPropertyName("short_description")] string? ShortDescription,
    [property: JsonPropertyName("header_image")] string? HeaderImage,
    IReadOnlyList<string>? Developers,
    IReadOnlyList<string>? Publishers,
    IReadOnlyList<SteamGenreResponse>? Genres,
    [property: JsonPropertyName("release_date")] SteamReleaseDateResponse? ReleaseDate
);

public record SteamGenreResponse(string? Id, string? Description);

public record SteamReleaseDateResponse(
    [property: JsonPropertyName("coming_soon")] bool ComingSoon,
    string? Date
);

public record SteamStoreSearchResponse(
    int Total,
    IReadOnlyList<SteamStoreSearchItemResponse>? Items
);

public record SteamStoreSearchItemResponse(long Id, string? Type, string? Name);
