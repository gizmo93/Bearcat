using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DoodStream.Api;

public record FileInfoResult(
    [property: JsonPropertyName("filecode")] string FileCode,
    [property: JsonPropertyName("status")] int Status
);
