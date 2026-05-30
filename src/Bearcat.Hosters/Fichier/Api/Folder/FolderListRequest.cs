using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.Folder;

public class FolderListRequest
{
    [JsonPropertyName("folder_id")]
    public int? FolderId { get; init; }
}
