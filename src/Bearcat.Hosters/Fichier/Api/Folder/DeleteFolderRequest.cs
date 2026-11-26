using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.Folder;

public class DeleteFolderRequest
{
    [JsonPropertyName("folder_id")]
    public int FolderId { get; init; }
}
