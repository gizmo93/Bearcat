using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

public interface IProxyServerWriteRepository
{
    Task<ProxyServer> GetByIdAsync(int proxyServerId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        string name,
        int? excludedProxyServerId,
        CancellationToken cancellationToken
    );

    void Add(ProxyServer proxyServer);

    void Remove(ProxyServer proxyServer);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
