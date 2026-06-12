using Bearcat.Abstractions.SeriesDatabase;

namespace Bearcat.SeriesDatabases.Tvdb;

public record TvdbConfig(string ApiKey) : ISeriesDatabaseConfig
{
    public const string ApiKeyConfigKey = "ApiKey";

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [ApiKeyConfigKey] = ApiKey };
    }
}
