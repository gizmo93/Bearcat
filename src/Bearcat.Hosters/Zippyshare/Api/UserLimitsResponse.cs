using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Zippyshare.Api;

public record UserLimitsResponse(
    [property: JsonPropertyName("storage_total")] long? StorageTotal,
    [property: JsonPropertyName("storage_remaining")] long? StorageRemaining
);
