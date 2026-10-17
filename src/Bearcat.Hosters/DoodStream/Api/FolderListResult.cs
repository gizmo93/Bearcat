using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DoodStream.Api;

public record FolderListResult(
    [property: JsonPropertyName("folders")] List<ListedFolderResult> Folders
);
