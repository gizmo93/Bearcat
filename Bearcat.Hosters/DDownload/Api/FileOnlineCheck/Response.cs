using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DDownload.Api.FileOnlineCheck;

public class Response
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("result")]
    public Result Result { get; set; } = null!;
}

public class Result
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("stats")]
    public Stats Stats { get; set; } = null!;

    [JsonPropertyName("files")]
    public Files[] Files { get; set; } = [];
}

public class Stats
{
    [JsonPropertyName("found")]
    public int Found { get; set; }

    [JsonPropertyName("not_found")]
    public int NotFound { get; set; }

    [JsonPropertyName("dmca")]
    public int Dmca { get; set; }
}

public class Files
{
    [JsonPropertyName("file_code")]
    public string FileCode { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("details")]
    public string? Name { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("downloads")]
    public long? Downloads { get; set; }

    [JsonPropertyName("uploaded")]
    public string? Uploaded { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }
}

