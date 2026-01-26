using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Rapidgator.ApiClient.File;

public class CheckLinksResponse
{
    [JsonPropertyName("response")] public List<ResponseObject>? Responses { get; set; }

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
