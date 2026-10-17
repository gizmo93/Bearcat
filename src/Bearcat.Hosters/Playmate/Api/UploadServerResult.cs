using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Playmate.Api;

public record UploadServerResult(
    [property: JsonPropertyName("upload_url")] string UploadUrl,
    [property: JsonPropertyName("key_field")] string KeyFieldName,
    [property: JsonPropertyName("file_field")] string FileFieldName
);
