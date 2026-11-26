using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.Alfafile.Api.Folder;

public class FolderResponse : IResponseWithStatus
{
    public ResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public Folder? Folder { get; set; }
    }

    public class Folder
    {
        [JsonPropertyName("folder_id")]
        public string? FolderId { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("nb_folders")]
        public int NbFolders { get; set; }

        [JsonPropertyName("nb_files")]
        public int NbFiles { get; set; }

        public IReadOnlyList<Folder> Folders { get; set; } = [];
    }
}
