using System.Text.Json.Serialization;

namespace BearCat.Core.Infrastructure.Hosters.Rapidgator.ApiClient.File;

public class UploadFileResponse
{
    public ResponseObject? Response { get; set; } = null!;

    public int Status { get; set; }

    public string? Details { get; set; }


    public class ResponseObject
    {
        public Upload? Upload { get; set; }

        public int State { get; set; }
    }

    public class Upload
    {
        [JsonPropertyName("upload_id")] public string UploadId { get; set; } = null!;


        public string Url { get; set; } = null!;

        public File? File { get; set; }

        public int State { get; set; }

        [JsonPropertyName("state_label")] public string StateLabel { get; set; } = null!;
    }

    public class File
    {
        [JsonPropertyName("file_id")] public string FileId { get; set; } = null!;

        public int Mode { get; set; }

        [JsonPropertyName("mode_label")] public string ModeLabel { get; set; } = null!;

        [JsonPropertyName("folder_id")] public string? FolderId { get; set; }

        [JsonPropertyName("name")] public string Name { get; set; } = null!;

        public string Hash { get; set; } = null!;

        public long Size { get; set; }

        public long Created { get; set; }

        public string Url { get; set; } = null!;
    }
}
