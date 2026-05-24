using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class FileInfoResponse
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("result")]
    public FileInfoResult[] Results { get; set; } = [];
}

public class FileInfoResult
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("filecode")]
    public string? FileCode { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
