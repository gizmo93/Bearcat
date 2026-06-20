using Bearcat.Abstractions.DistributionSite;

namespace Bearcat.DistributionSites.Extensions;

public static class ConfigExtensions
{
    public static T As<T>(this IDistributionSiteConfig config)
        where T : IDistributionSiteConfig
    {
        if (config is T typed)
        {
            return typed;
        }

        throw new InvalidOperationException(
            $"Expected a configuration of type {typeof(T).Name} but received {config.GetType().Name}."
        );
    }
}
