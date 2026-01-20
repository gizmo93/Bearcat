using BearCat.Core.Domain.Abstractions.Archiver;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Infrastructure.Archivers;

public class ArchiverFactory(IServiceProvider serviceProvider) : IArchiverFactory
{
    public IArchiver GetByName(string name)
    {
        return serviceProvider.GetRequiredKeyedService<IArchiver>(name);
    }
    
    public IReadOnlyList<ArchiverDto> GetArchivers()
    {
        var archivers = serviceProvider.GetKeyedServices<IArchiver>(KeyedService.AnyKey);
        return archivers
            .Select(a => new ArchiverDto(
                Name: a.Name,
                ClassName: a.GetType().Name,
                FileExtension: a.FileExtension))
            .ToList();
    }
}
