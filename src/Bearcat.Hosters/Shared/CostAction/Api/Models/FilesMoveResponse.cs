using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FilesMoveResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("files")] JsonElement Files,
    [property: JsonPropertyName("errors")] JsonElement Errors
) : ICostActionResponse;
