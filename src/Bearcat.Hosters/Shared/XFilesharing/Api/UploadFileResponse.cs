using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class UploadFileResponse
{
    [JsonPropertyName("file_status")]
    public string FileStatus { get; set; } = null!;

    [JsonPropertyName("file_code")]
    public string FileCode { get; set; } = null!;
}
