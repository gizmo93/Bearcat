using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FolderCreateRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("can_be_copied")] bool CanBeCopied,
    [property: JsonPropertyName("apptype")] string AppType,
    [property: JsonPropertyName("parent_folder_id")] long? ParentFolderId
);
