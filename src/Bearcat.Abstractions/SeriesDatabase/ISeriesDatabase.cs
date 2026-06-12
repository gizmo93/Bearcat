namespace Bearcat.Abstractions.SeriesDatabase;

public interface ISeriesDatabase
{
    string Name { get; }

    int ResolutionPriority { get; }

    IReadOnlyList<string> ConfigurationKeys { get; }

    Task<SeriesInfo?> GetSeriesInfoByImdbIdAsync(
        ISeriesDatabaseConfig config,
        string imdbId,
        CancellationToken cancellationToken = default
    );

    Task<SeriesInfo?> GetSeriesInfoByTitleAsync(
        ISeriesDatabaseConfig config,
        string title,
        CancellationToken cancellationToken = default
    );

    Task<TryLoginResult> TryLoginAsync(
        ISeriesDatabaseConfig config,
        CancellationToken cancellationToken = default
    );

    string SerializeConfig(IReadOnlyDictionary<string, string> config);

    ISeriesDatabaseConfig DeserializeConfig(string serializedConfig);
}
