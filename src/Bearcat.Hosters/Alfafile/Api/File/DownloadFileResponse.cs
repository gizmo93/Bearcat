using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.Alfafile.Api.File;

public class DownloadFileResponse : IResponseWithStatus
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
