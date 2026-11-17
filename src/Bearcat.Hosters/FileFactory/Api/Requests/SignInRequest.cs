using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record SignInRequest(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("args")] SignInArguments Arguments
);
