using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.ApiClient.SearchContainers;

public class Request
{
    [JsonPropertyName("search")]
    public string? Search { get; set; }
    
    [JsonPropertyName("primary_type")]
    public string? PrimaryType { get; set; }
    
    [JsonPropertyName("access_status")]
    public string? AccessStatus { get; set; }
    
    [JsonPropertyName("offset")]
    public int Offset { get; set; }
    
    [JsonPropertyName("limit")]
    public int Limit { get; set; }
    
    [JsonPropertyName("order_by")]
    public string? OrderBy { get; set; }
    
    [JsonPropertyName("order_type")]
    public string? OrderType { get; set; }
}
