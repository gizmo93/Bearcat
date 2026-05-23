using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class AccountInfoResponse
{
    public string Status { get; set; } = null!;

    public int Code { get; set; }

    [JsonPropertyName("available_traffic")]
    public long? AvailableTraffic { get; set; }

    [JsonPropertyName("account_expires")]
    public DateTimeOffset? AccountExpires { get; set; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    public string? Message { get; set; }
}
