using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Requests;

public record CreateFolderRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parentPath")] string ParentPath
);
