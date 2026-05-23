using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class LoginResponse
{
    public string Status { get; set; } = null!;

    public int Code { get; set; }

    [JsonPropertyName("auth_token")]
    public string? AuthToken { get; set; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    public string? Message { get; set; }
}
