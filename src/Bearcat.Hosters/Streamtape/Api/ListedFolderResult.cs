using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Streamtape.Api;

public record ListedFolderResult(
    [property: JsonPropertyName("id")] string FolderId,
    [property: JsonPropertyName("name")] string Name
);
