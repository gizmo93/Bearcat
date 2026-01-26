using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.ApiClient.SearchContainers;

public class Response
{
    [JsonPropertyName("total")]
    public long Total { get; set; }
}
