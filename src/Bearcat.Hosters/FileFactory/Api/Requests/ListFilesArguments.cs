using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record ListFilesArguments(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("folderId")] string FolderId,
    [property: JsonPropertyName("paginationOpts")] PaginationOptions PaginationOptions
);
