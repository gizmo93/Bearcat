using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.Ping;

public class RequestBody
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;
}
