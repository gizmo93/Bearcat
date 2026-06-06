using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.ImgBb.Api;

public class UploadResponse
{
    [JsonPropertyName("data")]
    public UploadData? Data { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("error")]
    public UploadError? Error { get; init; }
}

public class UploadData
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("display_url")]
    public string? DisplayUrl { get; init; }

    [JsonPropertyName("delete_url")]
    public string? DeleteUrl { get; init; }

    [JsonPropertyName("image")]
    public UploadedImage? Image { get; init; }

    [JsonPropertyName("thumb")]
    public UploadedImage? Thumbnail { get; init; }

    [JsonPropertyName("medium")]
    public UploadedImage? Medium { get; init; }
}

public class UploadedImage
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

public class UploadError
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
