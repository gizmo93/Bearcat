using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class FileInfoResult
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("filecode")]
    public string? FileCode { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string? Size { get; set; }

    [JsonPropertyName("downloads")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string? Downloads { get; set; }

    [JsonPropertyName("download")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string? Download { get; set; }
}
