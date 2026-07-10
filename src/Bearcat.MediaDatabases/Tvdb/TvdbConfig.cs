using Bearcat.Abstractions.MediaMetadataDatabase;

namespace Bearcat.MediaDatabases.Tvdb;

public record TvdbConfig(string ApiKey) : IMediaMetadataDatabaseConfig
{
    public const string ApiKeyConfigKey = "ApiKey";

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [ApiKeyConfigKey] = ApiKey };
    }
}
