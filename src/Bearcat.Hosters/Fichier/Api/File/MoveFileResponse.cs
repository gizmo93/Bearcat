using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.File;

public class MoveFileResponse
{
    public string? Status { get; init; }

    [JsonPropertyName("moved")]
    public int? Moved { get; init; }

    public string? Message { get; init; }
}
