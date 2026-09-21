using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.LoePic.Api;

public class LoePicError
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
