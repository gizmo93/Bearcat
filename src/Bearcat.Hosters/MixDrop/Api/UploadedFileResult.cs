using System.Text.Json.Serialization;

namespace Bearcat.Hosters.MixDrop.Api;

public record UploadedFileResult(
    [property: JsonPropertyName("fileref")] string FileReference,
    [property: JsonPropertyName("url")] string FileUrl,
    [property: JsonPropertyName("embedurl")] string? EmbedUrl
);
