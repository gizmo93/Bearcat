using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.KeepLinks.Api.ProtectLinks;

public class Response
{
    [JsonPropertyName("p_links")]
    public string? ContainerLink { get; set; }

    [JsonPropertyName("r_links")]
    public string? RemoveLink { get; set; }

    [JsonPropertyName("api_error")]
    public string? ApiError { get; set; }
}
