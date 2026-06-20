namespace Bearcat.Abstractions.DistributionSite;

public interface IDistributionSiteConfig
{
    IReadOnlyDictionary<string, string> ToDictionary();
}
