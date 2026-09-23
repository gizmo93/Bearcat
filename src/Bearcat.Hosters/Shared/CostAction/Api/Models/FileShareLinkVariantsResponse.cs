using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FileShareLinkVariantsResponse(
    [property: JsonPropertyName("standard")] string? Standard,
    [property: JsonPropertyName("with_file_name")] string? WithFileName
);
