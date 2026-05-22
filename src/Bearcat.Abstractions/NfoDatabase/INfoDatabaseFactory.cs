namespace Bearcat.Abstractions.NfoDatabase;

public interface INfoDatabaseFactory
{
    IReadOnlyList<NfoDatabaseDto> GetNfoDatabases();

    INfoDatabase Get(string className);

    IReadOnlyDictionary<string, INfoDatabase> GetByClassName();
}
