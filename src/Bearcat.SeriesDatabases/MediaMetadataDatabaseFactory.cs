using Bearcat.Abstractions.MediaMetadataDatabase;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.SeriesDatabases;

public class MediaMetadataDatabaseFactory(IServiceProvider serviceProvider)
    : IMediaMetadataDatabaseFactory
{
    public IReadOnlyList<MediaMetadataDatabaseDto> GetDatabases()
    {
        return serviceProvider
            .GetKeyedServices<IMediaMetadataDatabase>(KeyedService.AnyKey)
            .OrderBy(database => database.Name)
            .Select(database => new MediaMetadataDatabaseDto(
                database.Name,
                database.GetType().Name,
                database.ConfigurationKeys
            ))
            .ToList();
    }

    public IMediaMetadataDatabase Get(string className)
    {
        return serviceProvider.GetRequiredKeyedService<IMediaMetadataDatabase>(className);
    }

    public IReadOnlyDictionary<string, IMediaMetadataDatabase> GetByClassName()
    {
        return serviceProvider
            .GetKeyedServices<IMediaMetadataDatabase>(KeyedService.AnyKey)
            .ToDictionary(database => database.GetType().Name);
    }
}
