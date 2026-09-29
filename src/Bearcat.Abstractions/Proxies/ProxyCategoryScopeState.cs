namespace Bearcat.Abstractions.Proxies;

public record ProxyCategoryScopeState(
    ProxyCategory Category,
    ProxySelection ProxySelection,
    int? ProxyServerId
);
