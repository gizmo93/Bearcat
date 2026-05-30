using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Alfafile.Api.Folder;

public class FolderResponse
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

        public IReadOnlyList<Folder> Folders { get; set; } = [];
    }
}
