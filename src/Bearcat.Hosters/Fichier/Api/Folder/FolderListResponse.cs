using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.Folder;

public class FolderListResponse
{
    [JsonPropertyName("folder_id")]
    public int? FolderId { get; init; }

    public string? Name { get; init; }

    public string? Status { get; init; }

    public string? Message { get; init; }

    [JsonPropertyName("sub_folders")]
    public IReadOnlyList<Folder> SubFolders { get; init; } = [];

    public class Folder
    {
        public int? Id { get; init; }

        public string? Name { get; init; }
    }
}
