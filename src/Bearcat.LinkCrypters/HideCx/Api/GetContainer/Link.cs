using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.Api.GetContainer;

public class Link
{
    [JsonPropertyName("link_type")]
    public string LinkType { get; set; } = null!;

    [JsonPropertyName("hoster_url")]
    public string? HosterUrl { get; set; }
}
