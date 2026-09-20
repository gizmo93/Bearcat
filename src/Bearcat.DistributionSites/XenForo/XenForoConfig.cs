using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.XenForo;

public record XenForoConfig : IXenForoDistributionSiteConfig
{
    public string BaseUrl { get; init; } = null!;

    public string Username { get; init; } = null!;

    public string Password { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary() =>
        new Dictionary<string, string>
        {
            [nameof(BaseUrl)] = BaseUrl,
            [nameof(Username)] = Username,
            [nameof(Password)] = Password,
        };
}
