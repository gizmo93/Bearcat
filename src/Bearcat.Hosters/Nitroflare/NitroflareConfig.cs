using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Nitroflare;

public record NitroflareConfig : IHosterConfig
{
    public string UserHash { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(UserHash)] = UserHash };
    }
}
