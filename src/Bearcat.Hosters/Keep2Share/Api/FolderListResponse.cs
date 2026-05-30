using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public record FolderListResponse
{
    public string? Status { get; init; }

    public int Code { get; init; }

    public string? Message { get; init; }

    [JsonPropertyName("foldersList")]
    public IReadOnlyList<string> FoldersList { get; init; } = [];

    [JsonPropertyName("foldersIds")]
    public IReadOnlyList<string> FoldersIds { get; init; } = [];
}
