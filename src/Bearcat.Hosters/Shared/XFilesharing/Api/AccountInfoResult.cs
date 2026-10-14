using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class AccountInfoResult
{
    [JsonPropertyName("storage_left")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string StorageLeft { get; set; } = null!;

    [JsonPropertyName("storage_used")]
    [JsonConverter(typeof(StringOrNumberJsonConverter))]
    public string? StorageUsed { get; set; }
}
