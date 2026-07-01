using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared.XFilesharing.Api;

namespace Bearcat.Hosters.DDownload.Api;

public class FileCheckResponse
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("result")]
    public FileCheckResult Result { get; set; } = new();
}

public class FileCheckResult
{
    [JsonPropertyName("files")]
    public FileCheckFile[] Files { get; set; } = [];
}

public class FileCheckFile
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("file_code")]
    public string? FileCode { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("downloads")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string? Downloads { get; set; }
}
