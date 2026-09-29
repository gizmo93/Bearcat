using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

namespace Bearcat.Domain.UnitTest.UseCases.ManageProxyServers;

public class FakeProxyServerWriteRepository : IProxyServerWriteRepository
{
    public List<ProxyServer> ProxyServers { get; } = [];

    public Dictionary<int, List<ProxyCategory>> CategoryDefaultsByProxyServerId { get; } = [];

    public Dictionary<
        int,
        List<ProxyServerRegistrationUsageReadModel>
    > RegistrationUsagesByProxyServerId { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<ProxyServer> GetByIdAsync(int proxyServerId, CancellationToken cancellationToken)
    {
        return Task.FromResult(ProxyServers.Single(proxyServer => proxyServer.Id == proxyServerId));
    }

    public Task<bool> NameExistsAsync(
        string name,
        int? excludedProxyServerId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            ProxyServers.Any(proxyServer =>
                proxyServer.Name == name && proxyServer.Id != excludedProxyServerId
            )
        );
    }

    public Task<bool> HostAndPortExistAsync(
        string host,
        int port,
        int? excludedProxyServerId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            ProxyServers.Any(proxyServer =>
                string.Equals(proxyServer.Host, host, StringComparison.OrdinalIgnoreCase)
                && proxyServer.Port == port
                && proxyServer.Id != excludedProxyServerId
            )
        );
    }

    public Task<ProxyServerUsageReadModel> GetUsageAsync(
        int proxyServerId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            new ProxyServerUsageReadModel(
                CategoryDefaultsByProxyServerId.GetValueOrDefault(proxyServerId) ?? [],
                RegistrationUsagesByProxyServerId.GetValueOrDefault(proxyServerId) ?? []
            )
        );
    }

    public void Add(ProxyServer proxyServer)
    {
        proxyServer.Id =
            ProxyServers.Count == 0 ? 1 : ProxyServers.Max(existing => existing.Id) + 1;
        ProxyServers.Add(proxyServer);
    }

    public void Remove(ProxyServer proxyServer)
    {
        ProxyServers.Remove(proxyServer);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
