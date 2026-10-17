using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Playmate.Api;

public record FileInfoResult(
    [property: JsonPropertyName("file_code")] string FileCode,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("state")] string? State
);
