using Bearcat.Abstractions.MediaMetadataDatabase;

namespace Bearcat.MediaDatabases.Steam;

public record SteamConfig : IMediaMetadataDatabaseConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>();
    }
}
