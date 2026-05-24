using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.KrakenFiles;

public record KrakenFilesConfig : IHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
