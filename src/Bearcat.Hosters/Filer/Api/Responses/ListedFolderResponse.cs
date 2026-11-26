using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record ListedFolderResponse(
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("editCode")] string EditCode,
    [property: JsonPropertyName("fileCount")] int? FileCount,
    [property: JsonPropertyName("actualFileCount")] int? ActualFileCount,
    [property: JsonPropertyName("children")] IReadOnlyList<JsonElement>? Children
);
