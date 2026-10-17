using System.Text.Json.Serialization;

namespace Bearcat.Hosters.MixDrop.Api;

public record ListedFolderResult([property: JsonPropertyName("id")] long FolderId, string Title);
