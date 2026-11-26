using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FolderChildrenResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("data")] IReadOnlyList<JsonElement>? Data
) : ICostActionResponse;
