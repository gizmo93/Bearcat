using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.Api.CreateContainer;

public class Response
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("canonical_url")]
    public string CanonicalUrl { get; set; } = null!;
}
