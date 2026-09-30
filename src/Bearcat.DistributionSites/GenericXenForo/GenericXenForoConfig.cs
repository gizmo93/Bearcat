using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.GenericXenForo;

public sealed record GenericXenForoConfig : IXenForoDistributionSiteConfig
{
    public required string BaseUrl { get; init; }

    public required string Username { get; init; }

    public required string Password { get; init; }

    public IReadOnlyDictionary<string, object?> ToDictionary() =>
        new Dictionary<string, object?>
        {
            [nameof(BaseUrl)] = BaseUrl,
            [nameof(Username)] = Username,
            [nameof(Password)] = Password,
        };
}
