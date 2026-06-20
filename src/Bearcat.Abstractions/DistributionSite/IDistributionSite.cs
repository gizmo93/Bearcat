using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Abstractions.DistributionSite;

public interface IDistributionSite
{
    string Name { get; }

    PostContentFormat ContentFormat { get; }

    IReadOnlyList<string> ConfigurationKeys { get; }

    IDistributionSiteConfig DeserializeConfig(string serializedConfig);

    string SerializeConfig(Dictionary<string, string> config);

    Task<DistributionSession?> LogInAsync(
        IDistributionSiteConfig config,
        CancellationToken cancellationToken
    );

    Task<bool> IsSessionValidAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    );
}
