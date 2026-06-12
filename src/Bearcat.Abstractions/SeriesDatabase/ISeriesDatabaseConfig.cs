namespace Bearcat.Abstractions.SeriesDatabase;

public interface ISeriesDatabaseConfig
{
    IReadOnlyDictionary<string, string> ToDictionary();
}
