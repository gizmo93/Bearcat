using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.LoePic.Api;

public class AccountResponse
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("key_label")]
    public string? KeyLabel { get; init; }

    [JsonPropertyName("key_prefix")]
    public string? KeyPrefix { get; init; }

    [JsonPropertyName("image_count")]
    public int ImageCount { get; init; }

    [JsonPropertyName("total_views")]
    public int TotalViews { get; init; }
}
