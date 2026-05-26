namespace Bearcat.Abstractions.NfoDatabase;

public interface INfoDatabase
{
    string Name { get; }

    int ResolutionPriority { get; }

    IReadOnlyList<string> ConfigurationKeys { get; }

    Task<ReleaseInfo?> GetReleaseInfoAsync(
        INfoDatabaseConfig config,
        string dirname,
        CancellationToken cancellationToken = default
    );

    string SerializeConfig(IReadOnlyDictionary<string, string> config);

    INfoDatabaseConfig DeserializeConfig(string serializedConfig);
}
