using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Responses;

public record CompleteUploadResponse(
    [property: JsonPropertyName("fileId")] string? FileId,
    [property: JsonPropertyName("viewhash")] string? Viewhash,
    [property: JsonPropertyName("downloadUrl")] string? DownloadUrl
);
