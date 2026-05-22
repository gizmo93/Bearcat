using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.NfoDatabases.Xrel;

public record XrelConfig : INfoDatabaseConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>();
    }
}
