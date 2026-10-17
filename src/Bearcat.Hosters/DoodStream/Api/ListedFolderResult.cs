using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DoodStream.Api;

public record ListedFolderResult(
    [property: JsonPropertyName("fld_id")] string FolderId,
    [property: JsonPropertyName("name")] string Name
);
