using System.Net;

namespace Bearcat.Abstractions.Proxies;

public interface IProxyRoutingCache
{
    ResolvedProxyServer? GetProxyServerForCategory(ProxyCategory category);

    NetworkCredential? GetCredentialForProxyAddress(Uri proxyUri);

    Task RefreshAsync(CancellationToken cancellationToken);
}
