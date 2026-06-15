using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.PixelFox.Api;

public class UploadResponse
{
    [JsonPropertyName("image_uuid")]
    public string? ImageUuid { get; init; }

    [JsonPropertyName("view_url")]
    public string? ViewUrl { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("stable_url")]
    public string? StableUrl { get; init; }

    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; init; }

    [JsonPropertyName("duplicate")]
    public bool Duplicate { get; init; }

    // Derivatives are generated asynchronously. stable_variants exposes predictable URLs grouped by
    // family (original, webp, avif) and size (original, medium, small), each with a readiness flag.
    [JsonPropertyName("stable_variants")]
    public Dictionary<string, Dictionary<string, UploadVariant>>? StableVariants { get; init; }
}

public class UploadVariant
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("ready")]
    public bool Ready { get; init; }
}
