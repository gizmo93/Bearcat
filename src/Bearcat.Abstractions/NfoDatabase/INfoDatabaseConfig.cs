namespace Bearcat.Abstractions.NfoDatabase;

public interface INfoDatabaseConfig
{
    IReadOnlyDictionary<string, string> ToDictionary();
}
