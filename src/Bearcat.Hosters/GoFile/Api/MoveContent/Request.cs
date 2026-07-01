using System.Text.Json.Serialization;

namespace Bearcat.Hosters.GoFile.Api.MoveContent;

public record Request(
    [property: JsonPropertyName("contentsId")] string ContentsId,
    [property: JsonPropertyName("folderId")] string FolderId
);
