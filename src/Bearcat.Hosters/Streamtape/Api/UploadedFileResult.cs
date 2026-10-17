using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Streamtape.Api;

public record UploadedFileResult(
    [property: JsonPropertyName("id")] string FileId,
    [property: JsonPropertyName("url")] string FileUrl
);
