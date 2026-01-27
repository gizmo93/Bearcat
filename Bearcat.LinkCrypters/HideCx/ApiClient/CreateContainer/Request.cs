using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.HideCx.ApiClient.CreateContainer;

public class Request
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("mirrors")]
    public string[][] Mirrors { get; set; } = [];
}
