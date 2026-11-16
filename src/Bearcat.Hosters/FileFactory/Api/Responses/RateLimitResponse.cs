using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record RateLimitResponse([property: JsonPropertyName("retryAfter")] double? RetryAfter);
