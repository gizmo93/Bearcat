using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Alfafile.Api.File;

public class DownloadFileResponse
{
    public ResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }
    }
}
