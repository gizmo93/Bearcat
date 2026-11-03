using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.Api.SearchContainers;

public class Container
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("access_status")]
    public string? AccessStatus { get; set; }

    [JsonPropertyName("canonical_url")]
    public string CanonicalUrl { get; set; } = null!;
}
