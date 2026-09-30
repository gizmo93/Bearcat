using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.Rapidgator.Api.Folder;

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
        public string FolderId { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("folders")]
        public List<Folder> Folders { get; set; } = [];
    }
}
