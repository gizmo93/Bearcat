using BearCat.Core.Domain.Abstractions.Archiver;
using Microsoft.Extensions.DependencyInjection;

namespace BearCat.Core.Infrastructure.Archivers;

public class ArchiverFactory(IServiceProvider serviceProvider) : IArchiverFactory
{
    public IArchiver GetByName(string name)
    {
        return serviceProvider.GetRequiredKeyedService<IArchiver>(name);
    }
}
