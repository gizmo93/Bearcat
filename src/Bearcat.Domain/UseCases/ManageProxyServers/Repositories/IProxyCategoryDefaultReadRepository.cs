using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

public interface IProxyCategoryDefaultReadRepository
{
    Task<IReadOnlyList<ProxyCategoryDefaultReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );
}
