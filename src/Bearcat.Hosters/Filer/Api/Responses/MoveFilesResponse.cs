using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record MoveFilesResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("movedFiles")] int MovedFiles
);
