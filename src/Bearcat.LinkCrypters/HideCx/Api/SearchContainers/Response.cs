using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.Api.SearchContainers;

public class Response
{
    [JsonPropertyName("total")]
    public long Total { get; set; }

    [JsonPropertyName("containers")]
    public List<Container> Containers { get; set; } = [];
}
