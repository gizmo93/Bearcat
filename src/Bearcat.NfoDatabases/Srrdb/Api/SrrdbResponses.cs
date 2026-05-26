using System.Text.Json.Serialization;

namespace Bearcat.NfoDatabases.Srrdb.Api;

public record SrrdbDetailsResponse(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("files")] IReadOnlyList<SrrdbFileResponse>? Files,
    [property: JsonPropertyName("archived-files")] IReadOnlyList<SrrdbFileResponse>? ArchivedFiles
);

public record SrrdbFileResponse(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("crc")] string? Crc
);

public record SrrdbImdbResponse(
    [property: JsonPropertyName("releases")] IReadOnlyList<SrrdbImdbReleaseResponse>? Releases,
    [property: JsonPropertyName("query")] string? Query
);

public record SrrdbImdbReleaseResponse(
    [property: JsonPropertyName("imdb")] string? Imdb,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("rating")] string? Rating,
    [property: JsonPropertyName("votes")] string? Votes
);

public record SrrdbNfoResponse(
    [property: JsonPropertyName("release")] string? Release,
    [property: JsonPropertyName("nfo")] IReadOnlyList<string>? Nfo,
    [property: JsonPropertyName("nfolink")] IReadOnlyList<string>? NfoLink
);
