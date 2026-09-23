using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record UploadUrlResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("expire")] string? Expire
) : ICostActionResponse;
