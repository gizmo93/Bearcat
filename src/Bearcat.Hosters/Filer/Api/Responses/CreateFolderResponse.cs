using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record CreateFolderResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("folder")] FolderResponse? Folder,
    [property: JsonPropertyName("message")] string? Message
);
