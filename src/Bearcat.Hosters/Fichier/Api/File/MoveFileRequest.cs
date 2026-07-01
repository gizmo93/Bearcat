using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.File;

public class MoveFileRequest
{
    [JsonPropertyName("urls")]
    public IReadOnlyList<string> Urls { get; init; } = [];

    [JsonPropertyName("destination_folder_id")]
    public int DestinationFolderId { get; init; }
}
