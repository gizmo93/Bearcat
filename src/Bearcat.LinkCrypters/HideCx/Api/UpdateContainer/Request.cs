using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.Api.UpdateContainer;

public class Request
{
    [JsonPropertyName("mirrors")]
    public IReadOnlyList<string> Mirrors { get; set; } = [];
}
