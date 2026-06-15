using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.PixHost.Api;

public class UploadImageResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("show_url")]
    public string? ShowUrl { get; init; }

    [JsonPropertyName("th_url")]
    public string? ThumbnailUrl { get; init; }
}
