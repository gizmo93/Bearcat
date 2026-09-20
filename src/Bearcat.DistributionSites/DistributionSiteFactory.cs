using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.DistributionSites;

public sealed class DistributionSiteFactory(IServiceProvider serviceProvider)
    : IDistributionSiteFactory
{
    public IReadOnlyList<DistributionSiteDto> GetDistributionSites()
    {
        return serviceProvider
            .GetKeyedServices<IDistributionSite>(KeyedService.AnyKey)
            .Select(site => new DistributionSiteDto(
                Name: site.Name,
                ClassName: site.GetType().Name,
                Kind: site is IForumDistributionSite
                    ? DistributionSiteKind.Forum
                    : DistributionSiteKind.Blog,
                ConfigurationFields: site.ConfigurationFields,
                ConfigurationHelpResourceKey: site.ConfigurationHelpResourceKey
            ))
            .ToList();
    }

    public IDistributionSite Get(string className)
    {
        return serviceProvider.GetRequiredKeyedService<IDistributionSite>(className);
    }

    public IDistributionSite Create(string className, string serializedConfig)
    {
        var site = Get(className);
        var config = site.DeserializeConfig(serializedConfig);

        return site.WithConfiguration(config);
    }
}
