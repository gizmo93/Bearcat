using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Playmate.Api;

public record PlaymateResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("msg")] string? Message,
    [property: JsonPropertyName("result")] JsonElement Result
);
