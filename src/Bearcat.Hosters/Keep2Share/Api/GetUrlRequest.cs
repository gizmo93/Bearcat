using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public record GetUrlRequest(
    [property: JsonPropertyName("auth_token")] string AuthToken,
    [property: JsonPropertyName("file_id")] string FileId
);
