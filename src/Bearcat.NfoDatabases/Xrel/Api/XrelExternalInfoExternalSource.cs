using System.Text.Json.Serialization;

namespace Bearcat.NfoDatabases.Xrel.Api;

public record XrelExternalInfoExternalSource(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("name")] string? Name
);
