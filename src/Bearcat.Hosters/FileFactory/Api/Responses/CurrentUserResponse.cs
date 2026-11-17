using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record CurrentUserResponse([property: JsonPropertyName("id")] string Id);
