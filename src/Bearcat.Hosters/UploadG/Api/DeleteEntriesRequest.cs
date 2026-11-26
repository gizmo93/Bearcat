using System.Text.Json.Serialization;

namespace Bearcat.Hosters.UploadG.Api;

public record DeleteEntriesRequest(
    [property: JsonPropertyName("entryIds")] IReadOnlyList<long> EntryIds,
    [property: JsonPropertyName("deleteForever")] bool DeleteForever
);
