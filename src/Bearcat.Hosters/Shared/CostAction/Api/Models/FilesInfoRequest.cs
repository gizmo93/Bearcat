using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FilesInfoRequest(
    [property: JsonPropertyName("apptype")] string AppType,
    [property: JsonPropertyName("files_ids")] IReadOnlyList<string> FileIds
);
