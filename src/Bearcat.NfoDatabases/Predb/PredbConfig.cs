using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.NfoDatabases.Predb;

public record PredbConfig : INfoDatabaseConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>();
    }
}
