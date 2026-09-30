using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.Rapidgator.Api.File;

public class CheckLinksResponse : IResponseWithStatus
{
    [JsonPropertyName("response")]
    public List<ResponseObject>? Responses { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public string Url { get; set; } = null!;
        public string Filename { get; set; } = null!;
        public long? Size { get; set; }
        public string Status { get; set; } = null!;
    }
}
