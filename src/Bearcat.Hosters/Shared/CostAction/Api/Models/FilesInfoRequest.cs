using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FilesInfoRequest(
    [property: JsonPropertyName("apptype")] string AppType,
    [property: JsonPropertyName("files_ids")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? FileIds,
    [property: JsonPropertyName("folders_ids")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? FolderIds = null
);
