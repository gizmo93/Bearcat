using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Zippyshare.Api;

public record UserResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("limits")] UserLimitsResponse Limits
);
