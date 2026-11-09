using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record FolderSearchResponse(
    [property: JsonPropertyName("folders")] IReadOnlyList<FolderResponse>? Folders
);
