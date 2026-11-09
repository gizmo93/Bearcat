using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record DownloadLinkDataResponse(
    [property: JsonPropertyName("download_url")] string? DownloadUrl
);
