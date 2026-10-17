using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Playmate.Api;

public record ListedFolderResult(
    [property: JsonPropertyName("fld_id")] long FolderId,
    [property: JsonPropertyName("name")] string Name
);
