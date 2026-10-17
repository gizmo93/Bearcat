using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Vidara.Api;

public record FolderListResult(
    [property: JsonPropertyName("folders")] List<ListedFolderResult> Folders
);
