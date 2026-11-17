using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record LinkCheckResultResponse(
    [property: JsonPropertyName("originalUrl")] string? OriginalUrl,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("fileDetails")] LinkCheckFileDetailsResponse? FileDetails
);
