using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record FolderIdArguments([property: JsonPropertyName("folderId")] string FolderId);
