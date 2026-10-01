using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Rapidgator.Api.File;

public class DownloadFileResponse
{
    [JsonPropertyName("response")]
    public DownloadResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class DownloadResponseObject
    {
        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("delay")]
        public int Delay { get; set; }
    }
}
