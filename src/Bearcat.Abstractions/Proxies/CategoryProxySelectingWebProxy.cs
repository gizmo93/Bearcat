using System.Net;

namespace Bearcat.Abstractions.Proxies;

public class CategoryProxySelectingWebProxy(
    ProxyCategory clientCategory,
    IProxyRoutingCache proxyRoutingCache
) : IWebProxy
{
    public ICredentials? Credentials { get; set; } = new ProxyAddressCredentials(proxyRoutingCache);

    public Uri? GetProxy(Uri destination)
    {
        return SelectProxyServer()?.ProxyUri;
    }

    public bool IsBypassed(Uri host)
    {
        return SelectProxyServer() is null;
    }

    public ResolvedProxyServer? SelectProxyServer()
    {
        var category = ProxyCategoryScope.Current?.Category ?? clientCategory;

        return proxyRoutingCache.GetProxyServerForCategory(category);
    }
}
