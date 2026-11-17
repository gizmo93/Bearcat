using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record CreateFolderArguments(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("hash")] string Hash
);
