using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.ImageHosters;

public class ImageHosterFactory(IServiceProvider serviceProvider) : IImageHosterFactory
{
    public IReadOnlyList<ImageHosterDto> GetImageHosters()
    {
        return serviceProvider
            .GetKeyedServices<IImageHoster>(KeyedService.AnyKey)
            .Select(h => new ImageHosterDto(
                Name: h.Name,
                ClassName: h.GetType().Name,
                ConfigurationKeys: h.ConfigurationKeys,
                SupportsLogin: h is ISupportsLogin
            ))
            .ToList();
    }

    public IImageHoster Get(string className)
    {
        return serviceProvider.GetRequiredKeyedService<IImageHoster>(className);
    }

    public IReadOnlyDictionary<string, IImageHoster> GetByClassName()
    {
        return serviceProvider
            .GetKeyedServices<IImageHoster>(KeyedService.AnyKey)
            .ToDictionary(h => h.GetType().Name, h => h);
    }
}
