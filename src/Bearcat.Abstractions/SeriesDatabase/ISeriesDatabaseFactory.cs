namespace Bearcat.Abstractions.SeriesDatabase;

public interface ISeriesDatabaseFactory
{
    IReadOnlyList<SeriesDatabaseDto> GetSeriesDatabases();

    ISeriesDatabase Get(string className);

    IReadOnlyDictionary<string, ISeriesDatabase> GetByClassName();
}
