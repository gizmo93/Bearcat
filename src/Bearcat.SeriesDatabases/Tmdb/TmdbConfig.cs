using Bearcat.Abstractions.MediaMetadataDatabase;

namespace Bearcat.SeriesDatabases.Tmdb;

public record TmdbConfig(string ApiKey) : IMediaMetadataDatabaseConfig
{
    public const string ApiKeyConfigKey = "ApiKey";

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [ApiKeyConfigKey] = ApiKey };
    }
}
