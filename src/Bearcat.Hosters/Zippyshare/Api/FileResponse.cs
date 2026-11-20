using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Zippyshare.Api;

public record FileResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("file_id")] string? FileId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("downloads")] int? Downloads
);
