using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.FileCrypt.Api;

public class ContainerMirrorResponse
{
    [JsonPropertyName("links")]
    public List<string> Links { get; set; } = [];

    [JsonPropertyName("backup")]
    public List<List<string>> Backup { get; set; } = [];
}
