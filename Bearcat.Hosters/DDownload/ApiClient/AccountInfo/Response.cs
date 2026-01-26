using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DDownload.ApiClient.AccountInfo;

public class Response
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;
    
    [JsonPropertyName("status")]
    public int Status { get; set; }
    
    [JsonPropertyName("result")]
    public Result? Result { get; set; }
}

public class Result
{
    [JsonPropertyName("storage_left")]
    public string StorageLeft { get; set; } = null!;
    
    [JsonPropertyName("storage_used")]
    public long? StorageUsed { get; set; }
}

