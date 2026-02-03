using System.Text.Json.Serialization;

namespace Bearcat.Hosters.GoFile.Api.UploadFile;

public class Response
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;
    
    [JsonPropertyName("data")]
    public Data? Data { get; set; }
}

public class Data
{
    [JsonPropertyName("downloadPage")]
    public string DownloadUrl { get; set; } = null!;
}

