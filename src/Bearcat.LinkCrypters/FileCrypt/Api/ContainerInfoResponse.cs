using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.FileCrypt.Api;

public class ContainerInfoResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("container")]
    public Dictionary<string, ContainerMirrorResponse>? Container { get; set; }
}
