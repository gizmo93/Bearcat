using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.DataLoadMe;

public record DataLoadMeConfig : IXenForoDistributionSiteConfig
{
    public string Username { get; init; } = null!;

    public string Password { get; init; } = null!;

    public IReadOnlyDictionary<string, object?> ToDictionary() =>
        new Dictionary<string, object?>
        {
            [nameof(Username)] = Username,
            [nameof(Password)] = Password,
        };
}
