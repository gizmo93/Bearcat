using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.Upload;

public class EndUploadResponse
{
    public int Incoming { get; set; }

    public IReadOnlyList<UploadedLink> Links { get; set; } = [];

    public string? Status { get; set; }

    public string? Message { get; set; }

    [JsonIgnore]
    public string? RawContent { get; set; }

    public class UploadedLink
    {
        public string Download { get; set; } = string.Empty;

        public string Filename { get; set; } = string.Empty;

        public string? Remove { get; set; }

        public string Size { get; set; } = string.Empty;

        public string? Whirlpool { get; set; }
    }
}
