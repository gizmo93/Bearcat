using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class DirectLinkResult
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("size")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string? Size { get; set; }
}
