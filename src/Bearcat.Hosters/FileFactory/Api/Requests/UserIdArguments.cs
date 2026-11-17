using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record UserIdArguments([property: JsonPropertyName("userId")] string UserId);
