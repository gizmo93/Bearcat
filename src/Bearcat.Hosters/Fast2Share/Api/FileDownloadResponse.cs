using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fast2Share.Api;

public record FileDownloadResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("download_url")] string? DownloadUrl,
    [property: JsonPropertyName("seconds")] int? Seconds,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn
);
