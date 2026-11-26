using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record FilePageResponse(
    [property: JsonPropertyName("page")] IReadOnlyList<JsonElement> Page,
    [property: JsonPropertyName("isDone")] bool IsDone
);
