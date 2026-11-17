using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record MoveFilesArguments(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("fileIds")] IReadOnlyList<string> FileIds,
    [property: JsonPropertyName("targetFolderId")] string TargetFolderId
);
