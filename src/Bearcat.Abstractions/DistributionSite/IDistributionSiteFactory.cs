using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Abstractions.DistributionSite;

public interface IDistributionSiteFactory
{
    IReadOnlyList<DistributionSiteDto> GetDistributionSites();

    IDistributionSite Get(string className);

    IDistributionSite Create(string className, string serializedConfig);
}
