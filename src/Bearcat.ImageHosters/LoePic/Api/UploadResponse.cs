using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.LoePic.Api;

public class UploadResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("original_size")]
    public long OriginalSize { get; init; }

    [JsonPropertyName("webp_size")]
    public long WebpSize { get; init; }

    [JsonPropertyName("deduplicated")]
    public bool Deduplicated { get; init; }

    [JsonPropertyName("savings_pct")]
    public double SavingsPct { get; init; }

    [JsonPropertyName("links")]
    public UploadLinks? Links { get; init; }
}
