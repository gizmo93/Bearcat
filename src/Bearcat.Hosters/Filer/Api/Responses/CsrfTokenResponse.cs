using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Filer.Api.Responses;

public record CsrfTokenResponse([property: JsonPropertyName("csrfToken")] string? CsrfToken);
