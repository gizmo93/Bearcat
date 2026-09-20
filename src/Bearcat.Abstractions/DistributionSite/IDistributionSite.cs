using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Abstractions.DistributionSite;

public interface IDistributionSite
{
    string Name { get; }

    string BaseUrl { get; }

    PostContentFormat ContentFormat { get; }

    IReadOnlyList<DistributionSiteConfigurationField> ConfigurationFields { get; }

    string? ConfigurationHelpResourceKey { get; }

    IDistributionSiteConfig DeserializeConfig(string serializedConfig);

    string SerializeConfig(Dictionary<string, string> config);

    IDistributionSite WithConfiguration(IDistributionSiteConfig config);

    Task<DistributionSession?> LogInAsync(
        IDistributionSiteConfig config,
        CancellationToken cancellationToken
    );

    Task<bool> IsSessionValidAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    );
}
