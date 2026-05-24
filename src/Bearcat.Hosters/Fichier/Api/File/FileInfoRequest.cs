using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.File;

public class FileInfoRequest
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
