using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Nitroflare.Api.File;

public class FileInfoResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("result")]
    public FileInfoResult? Result { get; set; }
}

public class FileInfoResult
{
    [JsonPropertyName("files")]
    public IReadOnlyDictionary<string, NitroflareFile>? Files { get; set; }
}

public class NitroflareFile
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("uploadDate")]
    public string UploadDate { get; set; } = null!;

    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;

    [JsonPropertyName("premiumOnly")]
    public bool PremiumOnly { get; set; }
}
