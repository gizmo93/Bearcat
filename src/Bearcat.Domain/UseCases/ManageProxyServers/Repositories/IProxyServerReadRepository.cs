using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

public interface IProxyServerReadRepository
{
    Task<IReadOnlyList<ProxyServerReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ProxyServerRoutingReadModel>> GetAllForRoutingAsync(
        CancellationToken cancellationToken
    );

    Task<bool> ExistsAsync(int proxyServerId, CancellationToken cancellationToken);
}
