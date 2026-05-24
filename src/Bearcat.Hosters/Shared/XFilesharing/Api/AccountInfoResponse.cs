using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class AccountInfoResponse
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }
}
