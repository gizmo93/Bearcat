using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api;

public class ErrorResponseBody
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
