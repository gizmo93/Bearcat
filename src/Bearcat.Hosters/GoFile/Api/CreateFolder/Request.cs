using System.Text.Json.Serialization;

namespace Bearcat.Hosters.GoFile.Api.CreateFolder;

public record Request(
    [property: JsonPropertyName("parentFolderId")] string ParentFolderId,
    [property: JsonPropertyName("folderName")] string FolderName
);
