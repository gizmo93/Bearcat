using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FileInfoResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("is_deleted")] bool IsDeleted,
    [property: JsonPropertyName("download_count")] int? DownloadCount
);
