using Bearcat.Abstractions.MediaMetadataDatabase;

namespace Bearcat.SeriesDatabases.Tvdb;

public record TvdbConfig(string ApiKey) : IMediaMetadataDatabaseConfig
{
    public const string ApiKeyConfigKey = "ApiKey";

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [ApiKeyConfigKey] = ApiKey };
    }
}
