using Bearcat.Abstractions.ImageHoster;

namespace Bearcat.ImageHosters.PixHost;

public record PixHostConfig : IImageHosterConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>();
    }
}
