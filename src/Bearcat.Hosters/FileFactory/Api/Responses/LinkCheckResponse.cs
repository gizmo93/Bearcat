using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record LinkCheckResponse(
    [property: JsonPropertyName("results")] List<LinkCheckResultResponse>? Results
);
