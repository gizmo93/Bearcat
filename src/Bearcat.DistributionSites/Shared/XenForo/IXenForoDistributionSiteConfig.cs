using Bearcat.Abstractions.DistributionSite;

namespace Bearcat.DistributionSites.Shared.XenForo;

public interface IXenForoDistributionSiteConfig : IDistributionSiteConfig
{
    string Username { get; }

    string Password { get; }
}
