namespace Bearcat.Abstractions.Proxies;

public static class ProxyServerSelection
{
    public static ResolvedProxyServer? Select(
        ProxyCategory clientCategory,
        ProxyCategoryScopeState? scopeState,
        IProxyRoutingCache proxyRoutingCache
    )
    {
        if (scopeState is null || !BelongToSameGroup(scopeState.Category, clientCategory))
        {
            return proxyRoutingCache.GetProxyServerForCategory(clientCategory);
        }

        return SelectProxyServerOfScope(scopeState, proxyRoutingCache);
    }

    private static ResolvedProxyServer? SelectProxyServerOfScope(
        ProxyCategoryScopeState scopeState,
        IProxyRoutingCache proxyRoutingCache
    )
    {
        return scopeState.ProxySelection switch
        {
            ProxySelection.UseCategoryDefault => proxyRoutingCache.GetProxyServerForCategory(
                scopeState.Category
            ),
            ProxySelection.NoProxy => null,
            ProxySelection.SpecificProxyServer => proxyRoutingCache.GetProxyServerById(
                scopeState.ProxyServerId!.Value
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scopeState),
                scopeState.ProxySelection,
                null
            ),
        };
    }

    private static bool BelongToSameGroup(ProxyCategory scopeCategory, ProxyCategory clientCategory)
    {
        return ProxyCategoryGroupMapping.GetGroup(scopeCategory)
            == ProxyCategoryGroupMapping.GetGroup(clientCategory);
    }
}
