using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.Download;

public class DownloadTokenRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;
}
