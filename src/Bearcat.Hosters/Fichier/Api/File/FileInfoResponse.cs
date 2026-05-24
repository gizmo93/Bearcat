using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.File;

public class FileInfoResponse
{
    public string? Url { get; set; }

    public string? Filename { get; set; }

    public long? Size { get; set; }

    public string? Date { get; set; }

    [JsonPropertyName("content-type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentTypeAlternative { get; set; }

    public string? Status { get; set; }

    public string? Message { get; set; }
}
