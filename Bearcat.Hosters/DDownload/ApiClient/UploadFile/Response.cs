using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DDownload.ApiClient.UploadFile;


public class Response
{
    [JsonPropertyName("file_status")]
    public string FileStatus { get; set; } = null!;

    [JsonPropertyName("file_code")]
    public string FileCode { get; set; } = null!;
}

