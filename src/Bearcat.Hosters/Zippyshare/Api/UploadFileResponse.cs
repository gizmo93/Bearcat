using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Zippyshare.Api;

public record UploadFileResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("file_id")] string? FileId,
    [property: JsonPropertyName("download_link")] string? DownloadLink
);
