using System.Text.Json.Serialization;

namespace BearCat.Core.Infrastructure.Hosters.DDownload.ApiClient.UploadFile;


public class Response
{
    [JsonPropertyName("file_status")]
    public string FileStatus { get; set; } = null!;
    
    [JsonPropertyName("file_code")]
    public string FileCode { get; set; } = null!;
}

