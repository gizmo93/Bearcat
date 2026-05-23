using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Nitroflare.Api.File;

public class UploadFileResponse
{
    [JsonPropertyName("files")]
    public IReadOnlyList<UploadedFile>? Files { get; set; }
}

public class UploadedFile
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
