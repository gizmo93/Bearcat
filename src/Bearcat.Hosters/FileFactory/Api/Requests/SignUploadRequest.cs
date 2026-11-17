using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record SignUploadRequest(
    [property: JsonPropertyName("useSession")] bool UseSession,
    [property: JsonPropertyName("filename")] string FileName,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("folderId")] string? FolderId
);
