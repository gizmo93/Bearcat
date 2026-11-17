using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record FolderSelectOptionResponse(
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("level")] double Level,
    [property: JsonPropertyName("name")] string Name
);
