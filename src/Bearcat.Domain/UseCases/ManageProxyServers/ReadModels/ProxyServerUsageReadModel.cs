using Bearcat.Abstractions.Proxies;

namespace Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

public record ProxyServerUsageReadModel(IReadOnlyList<ProxyCategory> CategoryDefaults)
{
    public bool IsUsed => CategoryDefaults.Count > 0;
}
