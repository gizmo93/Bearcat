using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.GoFile;

public record GoFileConfig : IHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            { nameof(ApiKey), ApiKey }
        };
    }
}
