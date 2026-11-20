namespace Bearcat.Abstractions.Proxies;

public static class ProxyServerResolver
{
    public static ResolvedProxyServer? ResolveForClientCategory(
        ProxyCategory clientCategory,
        IProxyRoutingCache proxyRoutingCache
    )
    {
        var scopeState = ProxyCategoryScope.Current;

        if (scopeState is null || !AppliesToClientCategory(scopeState.Category, clientCategory))
        {
            return proxyRoutingCache.GetProxyServerForCategory(clientCategory);
        }

        return ResolveForScope(scopeState, proxyRoutingCache);
    }

    public static ResolvedProxyServer? ResolveForCurrentScope(IProxyRoutingCache proxyRoutingCache)
    {
        var scopeState = ProxyCategoryScope.Current;

        if (scopeState is null)
        {
            return null;
        }

        return ResolveForScope(scopeState, proxyRoutingCache);
    }

    private static ResolvedProxyServer? ResolveForScope(
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

    private static bool AppliesToClientCategory(
        ProxyCategory scopeCategory,
        ProxyCategory clientCategory
    )
    {
        return scopeCategory == clientCategory
            || (IsHosterCategory(scopeCategory) && IsHosterCategory(clientCategory));
    }

    private static bool IsHosterCategory(ProxyCategory category)
    {
        return category is ProxyCategory.HosterUploads or ProxyCategory.HosterMirrorDownloads;
    }
}
