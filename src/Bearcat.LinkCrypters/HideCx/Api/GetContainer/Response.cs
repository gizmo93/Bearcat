using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.Api.GetContainer;

public class Response
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("links")]
    public List<Link> Links { get; set; } = [];
}
