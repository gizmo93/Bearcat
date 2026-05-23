using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Alfafile.Api.File;

public class UploadedFile
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = null!;

    public int Mode { get; set; }

    [JsonPropertyName("mode_label")]
    public string ModeLabel { get; set; } = null!;

    [JsonPropertyName("folder_id")]
    public string? FolderId { get; set; }

    public string Name { get; set; } = null!;

    public string Hash { get; set; } = null!;

    public long Size { get; set; }

    public string Url { get; set; } = null!;

    public long Created { get; set; }
}
