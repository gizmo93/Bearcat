using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record FolderListResponse(
    [property: JsonPropertyName("folders")] IReadOnlyList<ListedFolderResponse> Folders,
    [property: JsonPropertyName("hasMore")] bool HasMore
);
