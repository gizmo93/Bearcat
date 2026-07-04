using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Alfafile.Api.Folder;

public class FolderContentResponse
{
    public ResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public FolderContent? Folder { get; set; }

        public Pager? Pager { get; set; }
    }

    public class FolderContent
    {
        [JsonPropertyName("folder_id")]
        public string? FolderId { get; set; }

        public IReadOnlyList<ContentFile> Files { get; set; } = [];
    }

    public class ContentFile
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = null!;

        [JsonPropertyName("nb_downloads")]
        public int? NbDownloads { get; set; }
    }

    public class Pager
    {
        public int Current { get; set; }

        public int Total { get; set; }
    }
}
