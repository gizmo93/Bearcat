using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record ConvexResponse<TValue>(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("value")] TValue? Value,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage
);
