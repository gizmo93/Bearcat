using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.LoePic.Api;

public class LoePicResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("error")]
    public LoePicError? Error { get; init; }
}
