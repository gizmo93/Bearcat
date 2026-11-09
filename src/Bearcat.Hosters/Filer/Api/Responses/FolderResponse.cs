using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record FolderResponse(
    [property: JsonPropertyName("hash")] string? Hash,
    [property: JsonPropertyName("name")] string? Name
);
