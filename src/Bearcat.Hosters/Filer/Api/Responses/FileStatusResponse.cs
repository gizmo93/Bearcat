using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record FileStatusResponse(
    [property: JsonPropertyName("file_hash")] string? FileHash,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("file_size")] long? FileSize
);
