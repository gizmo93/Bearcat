using System.Text.Json.Serialization;

namespace Bearcat.NfoDatabases.Predb.Api;

public record PredbNfoData(
    [property: JsonPropertyName("nfo")] string? Nfo,
    [property: JsonPropertyName("nfo_img")] string? NfoImage
);
