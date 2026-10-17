using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XVideoSharing.Api;

public record XVideoSharingResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("msg")] string? Msg,
    [property: JsonPropertyName("result")] JsonElement Result
);
