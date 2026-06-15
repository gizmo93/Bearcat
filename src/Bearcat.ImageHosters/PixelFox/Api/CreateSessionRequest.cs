using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.PixelFox.Api;

public record CreateSessionRequest(
    [property: JsonPropertyName("file_size")] long FileSize,
    [property: JsonPropertyName("is_nsfw")] bool IsNsfw,
    [property: JsonPropertyName("processing")] ProcessingRequest Processing
);

public record ProcessingRequest(
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("derivatives")] IReadOnlyList<DerivativeRequest> Derivatives
);

public record DerivativeRequest(
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("size")] string Size
);
