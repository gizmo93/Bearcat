using System.Text.Json.Serialization;

namespace Bearcat.Hosters.MixDrop.Api;

public record CreatedFolderResult([property: JsonPropertyName("id")] long FolderId);
