using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.Download;

public class DownloadTokenResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
