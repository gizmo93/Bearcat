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

public record XrelP2PRelease(
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
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("link_href")] string? LinkHref,
    [property: JsonPropertyName("uris")] IReadOnlyList<string>? Uris
);

public record XrelExternalInfoDetails(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("link_href")] string? LinkHref,
    [property: JsonPropertyName("genre")] string? Genre,
    [property: JsonPropertyName("cover_url")] string? CoverUrl,
    [property: JsonPropertyName("uris")] IReadOnlyList<string>? Uris,
    [property: JsonPropertyName("externals")] IReadOnlyList<XrelExternalInfoExternal>? Externals
);

public record XrelExternalInfoExternal([property: JsonPropertyName("plot")] string? Plot);

public record XrelExternalInfoMedia(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("url_full")] string? UrlFull,
    [property: JsonPropertyName("url_thumb")] string? UrlThumb
);
