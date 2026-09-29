using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

public interface IProxyCategoryDefaultWriteRepository
{
    Task<List<ProxyCategoryDefault>> GetAllForUpdateAsync(CancellationToken cancellationToken);

    void Add(ProxyCategoryDefault proxyCategoryDefault);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
