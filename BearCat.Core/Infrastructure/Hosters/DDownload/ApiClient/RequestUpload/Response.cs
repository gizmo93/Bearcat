using System.Text.Json.Serialization;

namespace BearCat.Core.Infrastructure.Hosters.DDownload.ApiClient.RequestUpload;

public class Response
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;
    
    [JsonPropertyName("status")]
    public int Status { get; set; }
    
    [JsonPropertyName("result")]
    public string? UploadUrl { get; set; }
    
    [JsonPropertyName("sess_id")]
    public string? SessionId { get; set; }
}

