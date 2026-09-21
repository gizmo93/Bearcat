using Bearcat.Abstractions.ImageHoster;

namespace Bearcat.ImageHosters.LoePic;

public record LoePicConfig : IImageHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
