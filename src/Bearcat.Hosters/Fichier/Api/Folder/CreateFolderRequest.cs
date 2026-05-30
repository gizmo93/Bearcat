using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.Folder;

public class CreateFolderRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("folder_id")]
    public int? FolderId { get; init; }
}
