using Bearcat.Abstractions.ImageHoster;

namespace Bearcat.ImageHosters.PixelFox;

public record PixelFoxConfig : IImageHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
