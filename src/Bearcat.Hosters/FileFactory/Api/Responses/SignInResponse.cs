using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record SignInResponse([property: JsonPropertyName("tokens")] SignInTokensResponse? Tokens);
