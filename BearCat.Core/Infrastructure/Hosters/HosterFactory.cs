using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.UseCases.ManageUploads;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Infrastructure.Hosters;

public class HosterFactory(
    IServiceProvider serviceProvider) : IHosterFactory
{
    public IHoster GetByName(string name)
    {
        return serviceProvider.GetRequiredKeyedService<IHoster>(name);
    }

    public IReadOnlyList<HosterReadModel> GetHosterReadModels()
    {
        var hosters = serviceProvider.GetKeyedServices<IHoster>(KeyedService.AnyKey);

        return hosters
            .Select(h => new HosterReadModel(
                Name: h.Name,
                HosterClassName: h.GetType().Name,
                ConfigurationKeys: h.ConfigurationKeys))
            .ToList();
    }

    public IReadOnlyDictionary<string, IHoster> GetHostersByName()
    {
        var hosters = serviceProvider.GetKeyedServices<IHoster>(KeyedService.AnyKey);
        return hosters.ToDictionary(h => h.Name);
    }
}
