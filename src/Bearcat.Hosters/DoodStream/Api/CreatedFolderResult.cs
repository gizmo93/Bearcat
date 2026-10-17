using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DoodStream.Api;

public record CreatedFolderResult([property: JsonPropertyName("fld_id")] string FolderId);
