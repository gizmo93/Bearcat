using Bearcat.Abstractions.Proxies;

namespace Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

public record ProxyServerUsageReadModel(
    IReadOnlyList<ProxyCategory> CategoryDefaults,
    IReadOnlyList<ProxyServerRegistrationUsageReadModel> Registrations
)
{
    public bool IsUsed => CategoryDefaults.Count > 0 || Registrations.Count > 0;
}
