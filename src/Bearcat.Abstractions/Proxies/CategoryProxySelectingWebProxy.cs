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
        return ResolveProxyServer()?.ProxyUri;
    }

    public bool IsBypassed(Uri host)
    {
        return ResolveProxyServer() is null;
    }

    private ResolvedProxyServer? ResolveProxyServer()
    {
        return ProxyServerResolver.ResolveForClientCategory(clientCategory, proxyRoutingCache);
    }
}
