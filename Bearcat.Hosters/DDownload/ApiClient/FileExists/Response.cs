using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DDownload.ApiClient.FileExists;

public class Response
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    public Result? Result { get; set; }
}

public class Result
{
    [JsonPropertyName("exists")]
    public int Exists { get; set; }

    [JsonPropertyName("matches")]
    public Matches[] Matches { get; set; } = [];
}

public class Matches
{
    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = null!;

    [JsonPropertyName("file_code")]
    public string FileCode { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("uploaded")]
    public string Uploaded { get; set; } = null!;

    [JsonPropertyName("fld_id")]
    public int FolderId { get; set; }

    [JsonPropertyName("link")]
    public string Link { get; set; } = null!;
}

