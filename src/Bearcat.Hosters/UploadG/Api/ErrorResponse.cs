using System.Text.Json.Serialization;

namespace Bearcat.Hosters.UploadG.Api;

public record ErrorResponse([property: JsonPropertyName("message")] string? Message);
