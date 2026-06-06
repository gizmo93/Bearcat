using Bearcat.Abstractions.ImageHoster;

namespace Bearcat.ImageHosters.Extensions;

public static class ConfigExtensions
{
    public static T As<T>(this IImageHosterConfig config)
        where T : IImageHosterConfig
    {
        return (T)config;
    }
}
