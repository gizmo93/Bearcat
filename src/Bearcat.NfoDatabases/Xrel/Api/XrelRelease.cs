using System.Text.Json.Serialization;

namespace Bearcat.NfoDatabases.Xrel.Api;

public record XrelRelease(
    [property: JsonPropertyName("dirname")] string? Dirname,
    [property: JsonPropertyName("link_href")] string? LinkHref,
    [property: JsonPropertyName("size")] XrelReleaseSize? Size,
    [property: JsonPropertyName("video_type")] string? VideoType,
    [property: JsonPropertyName("audio_type")] string? AudioType,
    [property: JsonPropertyName("ext_info")] XrelExternalInfo? ExtInfo
);

public record XrelP2pRelease(
    [property: JsonPropertyName("dirname")] string? Dirname,
    [property: JsonPropertyName("link_href")] string? LinkHref,
    [property: JsonPropertyName("size_mb")] int? SizeMb,
    [property: JsonPropertyName("ext_info")] XrelExternalInfo? ExtInfo
);

public record XrelReleaseSize(
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("unit")] string? Unit
);

public record XrelExternalInfo(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("link_href")] string? LinkHref,
    [property: JsonPropertyName("uris")] IReadOnlyList<string>? Uris
);
