using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record LinkCheckFileDetailsResponse([property: JsonPropertyName("name")] string? Name);
