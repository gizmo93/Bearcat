using Bearcat.Abstractions.NfoDatabase;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.NfoDatabases;

public class NfoDatabaseFactory(IServiceProvider serviceProvider) : INfoDatabaseFactory
{
    public IReadOnlyList<NfoDatabaseDto> GetNfoDatabases()
    {
        return serviceProvider
            .GetKeyedServices<INfoDatabase>(KeyedService.AnyKey)
            .OrderBy(database => database.Name)
            .Select(database => new NfoDatabaseDto(
                database.Name,
                database.GetType().Name,
                database.ConfigurationKeys
            ))
            .ToList();
    }

    public INfoDatabase Get(string className)
    {
        return serviceProvider.GetRequiredKeyedService<INfoDatabase>(className);
    }

    public IReadOnlyDictionary<string, INfoDatabase> GetByClassName()
    {
        return serviceProvider
            .GetKeyedServices<INfoDatabase>(KeyedService.AnyKey)
            .ToDictionary(database => database.GetType().Name);
    }
}
