using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Vidara.Api;

public record CreatedFolderResult([property: JsonPropertyName("folder_id")] long FolderId);
