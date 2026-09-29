using System.Net;
using Bearcat.Abstractions.Proxies;

namespace Bearcat.Abstractions.UnitTest.Proxies;

public class FakeProxyRoutingCache : IProxyRoutingCache
{
    public Dictionary<ProxyCategory, ResolvedProxyServer> ProxyServerByCategory { get; } = [];

    public Dictionary<Uri, NetworkCredential> CredentialByProxyAddress { get; } = [];

    public ResolvedProxyServer? GetProxyServerForCategory(ProxyCategory category)
    {
        return ProxyServerByCategory.GetValueOrDefault(category);
    }

    public NetworkCredential? GetCredentialForProxyAddress(Uri proxyUri)
    {
        return CredentialByProxyAddress.GetValueOrDefault(proxyUri);
    }

    public Task RefreshAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public ResolvedProxyServer RouteCategory(
        ProxyCategory category,
        string proxyUri,
        bool hasUnreadableSecrets = false
    )
    {
        var proxyServer = new ResolvedProxyServer(
            ProxyServerByCategory.Count + 1,
            $"Proxy for {category}",
            new Uri(proxyUri),
            Credential: null,
            hasUnreadableSecrets
        );
        ProxyServerByCategory[category] = proxyServer;

        return proxyServer;
    }
}
