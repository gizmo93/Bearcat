using System.Net;

namespace Bearcat.Abstractions.Proxies;

public class ProxyAddressCredentials(IProxyRoutingCache proxyRoutingCache) : ICredentials
{
    public NetworkCredential? GetCredential(Uri uri, string authType)
    {
        return proxyRoutingCache.GetCredentialForProxyAddress(uri);
    }
}
