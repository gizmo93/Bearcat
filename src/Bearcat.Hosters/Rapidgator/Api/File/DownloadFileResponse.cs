using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.Rapidgator.Api.File;

public class DownloadFileResponse : IResponseWithStatus
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
