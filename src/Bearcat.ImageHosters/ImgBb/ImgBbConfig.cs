using Bearcat.Abstractions.ImageHoster;

namespace Bearcat.ImageHosters.ImgBb;

public record ImgBbConfig : IImageHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
