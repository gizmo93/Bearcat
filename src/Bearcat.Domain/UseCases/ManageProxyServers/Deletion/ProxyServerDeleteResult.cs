using Bearcat.Abstractions.Proxies;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Deletion;

public record ProxyServerDeleteResult(
    bool IsDeleted,
    IReadOnlyList<ProxyCategory> UsingCategoryDefaults
);
