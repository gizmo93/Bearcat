using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class AccountInfoResponse
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("result")]
    public AccountInfoResult? Result { get; set; }
}

public class AccountInfoResult
{
    [JsonPropertyName("storage_left")]
    public string StorageLeft { get; set; } = null!;

    [JsonPropertyName("storage_used")]
    public long? StorageUsed { get; set; }
}
