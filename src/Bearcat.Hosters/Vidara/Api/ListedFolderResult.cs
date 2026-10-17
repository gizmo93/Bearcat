using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Vidara.Api;

public record ListedFolderResult(
    [property: JsonPropertyName("fld_id")] long FolderId,
    [property: JsonPropertyName("name")] string Name
);
