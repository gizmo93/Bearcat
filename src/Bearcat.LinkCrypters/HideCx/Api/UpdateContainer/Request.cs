using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.Api.UpdateContainer;

public class Request
{
    [JsonPropertyName("mirrors")]
    public string[][] Mirrors { get; set; } = [];
}
