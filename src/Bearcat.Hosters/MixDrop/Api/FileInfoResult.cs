using System.Text.Json.Serialization;

namespace Bearcat.Hosters.MixDrop.Api;

public record FileInfoResult(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("deleted")] bool IsDeleted
);
