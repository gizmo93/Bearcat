using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record SignInArguments(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("params")] SignInParameters Parameters
);
