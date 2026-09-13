using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Fast2Share;

public record Fast2ShareConfig : IHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
