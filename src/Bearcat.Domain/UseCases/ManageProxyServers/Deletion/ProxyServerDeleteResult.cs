using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Deletion;

public record ProxyServerDeleteResult(
    bool IsDeleted,
    IReadOnlyList<ProxyCategory> UsingCategoryDefaults,
    IReadOnlyList<ProxyServerRegistrationUsageReadModel> UsingRegistrations
);
