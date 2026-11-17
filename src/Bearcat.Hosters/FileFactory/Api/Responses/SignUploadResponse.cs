using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record SignUploadResponse(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sessionId")] string? SessionId
);
