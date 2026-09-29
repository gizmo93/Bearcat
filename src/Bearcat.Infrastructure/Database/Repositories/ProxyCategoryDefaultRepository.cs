using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ProxyCategoryDefaultRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IProxyCategoryDefaultReadRepository, IProxyCategoryDefaultWriteRepository
{
    public async Task<IReadOnlyList<ProxyCategoryDefaultReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ProxyCategoryDefaults.OrderBy(proxyCategoryDefault =>
                proxyCategoryDefault.ProxyCategory
            )
            .Select(proxyCategoryDefault => new ProxyCategoryDefaultReadModel(
                proxyCategoryDefault.ProxyCategory,
                proxyCategoryDefault.ProxyServerId
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProxyCategoryDefault>> GetAllForUpdateAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.ProxyCategoryDefaults.ToListAsync(cancellationToken);
    }

    public void Add(ProxyCategoryDefault proxyCategoryDefault)
    {
        dbWrite.Add(proxyCategoryDefault);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
