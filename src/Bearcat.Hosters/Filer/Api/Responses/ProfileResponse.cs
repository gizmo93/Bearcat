using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record ProfileResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("status")] string? Status
);
