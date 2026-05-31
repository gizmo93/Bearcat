using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api;

public class ApiRequest<TBody>
{
    [JsonPropertyName("apikey")]
    public string ApiKey { get; set; } = null!;

    [JsonPropertyName("body")]
    public TBody Body { get; set; } = default!;
}
