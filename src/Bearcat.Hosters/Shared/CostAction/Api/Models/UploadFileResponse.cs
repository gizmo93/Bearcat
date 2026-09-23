using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record UploadFileResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("delete_id")] string? DeleteId
) : ICostActionResponse;
