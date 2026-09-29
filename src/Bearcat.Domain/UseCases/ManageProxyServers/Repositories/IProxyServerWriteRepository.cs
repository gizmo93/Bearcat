using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

public interface IProxyServerWriteRepository
{
    Task<ProxyServer> GetByIdAsync(int proxyServerId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        string name,
        int? excludedProxyServerId,
        CancellationToken cancellationToken
    );

    Task<bool> HostAndPortExistAsync(
        string host,
        int port,
        int? excludedProxyServerId,
        CancellationToken cancellationToken
    );

    Task<ProxyServerUsageReadModel> GetUsageAsync(
        int proxyServerId,
        CancellationToken cancellationToken
    );

    void Add(ProxyServer proxyServer);

    void Remove(ProxyServer proxyServer);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
