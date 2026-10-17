using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Streamtape.Api;

public record FolderListResult(
    [property: JsonPropertyName("folders")] List<ListedFolderResult> Folders
);
