using System.Text.Json.Serialization;

namespace Bearcat.NfoDatabases.Predb.Api;

public record PredbReleaseResponse(
    [property: JsonPropertyName("release")] string? Release,
    [property: JsonPropertyName("section")] string? Section,
    [property: JsonPropertyName("size")] double? Size,
    [property: JsonPropertyName("group")] string? Group,
    [property: JsonPropertyName("genre")] string? Genre
);
