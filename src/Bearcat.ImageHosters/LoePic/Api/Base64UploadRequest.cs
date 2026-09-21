using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.LoePic.Api;

public record Base64UploadRequest(
    [property: JsonPropertyName("base64")] string Base64,
    [property: JsonPropertyName("name")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Name
);
