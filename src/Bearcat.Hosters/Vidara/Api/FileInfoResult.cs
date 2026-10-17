using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Vidara.Api;

public record FileInfoResult(
    [property: JsonPropertyName("filecode")] string FileCode,
    [property: JsonPropertyName("status")] string Status
);
