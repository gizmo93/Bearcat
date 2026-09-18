using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class GetUrlResponse
{
    public string Status { get; set; } = null!;

    public int Code { get; set; }

    public string? Url { get; set; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    public string? Message { get; set; }
}
