using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Zippyshare.Api;

public record ErrorResponse(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("message")] string? Message
);
