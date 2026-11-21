using Bearcat.Abstractions.DistributionSite;

namespace Bearcat.DistributionSites.WordPress;

public sealed record WordPressConfig : IDistributionSiteConfig
{
    public required string BaseUrl { get; init; }

    public required string Username { get; init; }

    public required string ApplicationPassword { get; init; }

    public IReadOnlyDictionary<string, object?> ToDictionary() =>
        new Dictionary<string, object?>
        {
            [nameof(BaseUrl)] = BaseUrl,
            [nameof(Username)] = Username,
            [nameof(ApplicationPassword)] = ApplicationPassword,
        };
}
