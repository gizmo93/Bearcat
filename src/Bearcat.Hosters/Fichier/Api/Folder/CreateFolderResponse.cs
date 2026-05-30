using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.Folder;

public class CreateFolderResponse
{
    public string? Status { get; init; }

    [JsonPropertyName("folder_id")]
    public int? FolderId { get; init; }

    public string? Name { get; init; }

    public string? Message { get; init; }
}
