using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.BoerseCx;

public record BoerseCxConfig : IXenForoDistributionSiteConfig
{
    public string Username { get; init; } = null!;

    public string Password { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary() =>
        new Dictionary<string, string>
        {
            [nameof(Username)] = Username,
            [nameof(Password)] = Password,
        };
}
