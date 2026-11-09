using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record DownloadLinkResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("data")] DownloadLinkDataResponse? Data
);
