using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record UploadedFileResponse(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("hash")] string? Hash,
    [property: JsonPropertyName("edit")] string? Edit,
    [property: JsonPropertyName("error")] JsonElement Error
);
