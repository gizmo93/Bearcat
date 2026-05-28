using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class CaptchaChallengeResponse
{
    public string Status { get; set; } = null!;

    public int Code { get; set; }

    public string? Challenge { get; set; }

    [JsonPropertyName("captcha_url")]
    public string? CaptchaUrl { get; set; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    public string? Message { get; set; }
}
