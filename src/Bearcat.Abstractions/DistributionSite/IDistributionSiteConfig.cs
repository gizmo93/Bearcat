namespace Bearcat.Abstractions.DistributionSite;

public interface IDistributionSiteConfig
{
    IReadOnlyDictionary<string, object?> ToDictionary();
}
