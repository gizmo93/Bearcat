using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.NfoDatabases.Srrdb;

public record SrrdbConfig : INfoDatabaseConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>();
    }
}
