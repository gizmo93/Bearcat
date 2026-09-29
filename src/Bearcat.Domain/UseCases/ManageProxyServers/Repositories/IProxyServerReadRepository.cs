using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

public interface IProxyServerReadRepository
{
    Task<IReadOnlyList<ProxyServerReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<ProxyServerReadModel?> GetReadModelAsync(
        int proxyServerId,
        CancellationToken cancellationToken = default
    );
}
