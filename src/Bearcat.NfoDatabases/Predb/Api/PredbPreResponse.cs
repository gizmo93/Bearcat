using System.Text.Json.Serialization;

namespace Bearcat.NfoDatabases.Predb.Api;

public record PredbPreResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("results")] int Results,
    [property: JsonPropertyName("data")] IReadOnlyList<PredbReleaseResponse>? Data
);
