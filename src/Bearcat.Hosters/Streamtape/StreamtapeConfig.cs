using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Streamtape;

public record StreamtapeConfig : IHosterConfig
{
    public string ApiLogin { get; init; } = null!;

    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            [nameof(ApiLogin)] = ApiLogin,
            [nameof(ApiKey)] = ApiKey,
        };
    }
}
