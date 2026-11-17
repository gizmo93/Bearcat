using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record SignInTokensResponse([property: JsonPropertyName("token")] string? Token);
