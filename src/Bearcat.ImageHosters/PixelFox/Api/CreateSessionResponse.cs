using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.PixelFox.Api;

public class CreateSessionResponse
{
    [JsonPropertyName("upload_url")]
    public string? UploadUrl { get; init; }

    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; init; }

    [JsonPropertyName("max_bytes")]
    public long MaxBytes { get; init; }
}
