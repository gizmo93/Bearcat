using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FilesMoveRequest(
    [property: JsonPropertyName("ids")] IReadOnlyList<string> Ids,
    [property: JsonPropertyName("folder_id")] string FolderId
);
