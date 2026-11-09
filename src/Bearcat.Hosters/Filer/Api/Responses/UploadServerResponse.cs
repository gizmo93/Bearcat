using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record UploadServerResponse(
    [property: JsonPropertyName("server")] string? Server,
    [property: JsonPropertyName("session")] string? Session,
    [property: JsonPropertyName("user_id")] long? UserId
);
