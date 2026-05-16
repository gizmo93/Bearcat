using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DDownload.Api.FileInfo;

public class Response
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("result")]
    public Result[] Results { get; set; } = [];
}

public class Result
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("filecode")]
    public string? FileCode { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
