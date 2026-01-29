using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.KeepLinks.ApiClient.GetLinks;

public class Response
{
    [JsonPropertyName("url_id")]
    public string? UrlId { get; set; }
    
    [JsonPropertyName("api_error")]
    public string? ApiError { get; set; }
}
