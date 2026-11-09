using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record MessageResponse([property: JsonPropertyName("message")] string? Message);
