using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Playmate.Api;

public record CreatedFolderResult([property: JsonPropertyName("fld_id")] long FolderId);
