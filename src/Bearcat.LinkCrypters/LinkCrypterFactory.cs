using Bearcat.Abstractions.LinkCrypter;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.LinkCrypters;

public class LinkCrypterFactory(IServiceProvider serviceProvider) : ILinkCrypterFactory
{
    public IReadOnlyList<LinkCrypterDto> GetLinkCrypters()
    {
        return serviceProvider
            .GetKeyedServices<ILinkCrypter>(KeyedService.AnyKey)
            .Select(l => new LinkCrypterDto(
                Name: l.Name,
                ClassName: l.GetType().Name,
                ConfigurationKeys: l.ConfigurationKeys,
                SupportsCaptcha: l.SupportsCaptcha,
                SupportsContainerDownload: l.SupportsContainerDownload,
                SupportsClickAndLoad: l.SupportsClickAndLoad
            ))
            .ToList();
    }

    public ILinkCrypter Get(string className)
    {
        return serviceProvider.GetRequiredKeyedService<ILinkCrypter>(className);
    }

    public IReadOnlyDictionary<string, ILinkCrypter> GetByClassName()
    {
        return serviceProvider
            .GetKeyedServices<ILinkCrypter>(KeyedService.AnyKey)
            .ToDictionary(l => l.GetType().Name, l => l);
    }
}
