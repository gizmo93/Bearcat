using Bearcat.Abstractions.SeriesDatabase;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.SeriesDatabases;

public class SeriesDatabaseFactory(IServiceProvider serviceProvider) : ISeriesDatabaseFactory
{
    public IReadOnlyList<SeriesDatabaseDto> GetSeriesDatabases()
    {
        return serviceProvider
            .GetKeyedServices<ISeriesDatabase>(KeyedService.AnyKey)
            .OrderBy(database => database.Name)
            .Select(database => new SeriesDatabaseDto(
                database.Name,
                database.GetType().Name,
                database.ConfigurationKeys
            ))
            .ToList();
    }

    public ISeriesDatabase Get(string className)
    {
        return serviceProvider.GetRequiredKeyedService<ISeriesDatabase>(className);
    }

    public IReadOnlyDictionary<string, ISeriesDatabase> GetByClassName()
    {
        return serviceProvider
            .GetKeyedServices<ISeriesDatabase>(KeyedService.AnyKey)
            .ToDictionary(database => database.GetType().Name);
    }
}
