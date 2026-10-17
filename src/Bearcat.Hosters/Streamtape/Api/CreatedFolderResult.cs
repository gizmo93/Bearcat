using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Streamtape.Api;

public record CreatedFolderResult([property: JsonPropertyName("folderid")] string FolderId);
