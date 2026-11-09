using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Requests;

public record LoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password
);
