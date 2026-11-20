namespace Bearcat.Abstractions.Proxies;

public class UnreadableProxySecretsRejectingHandler(
    ProxyCategory clientCategory,
    IProxyRoutingCache proxyRoutingCache
) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var proxyServer = ProxyServerResolver.ResolveForClientCategory(
            clientCategory,
            proxyRoutingCache
        );

        if (proxyServer is { HasUnreadableSecrets: true })
        {
            throw new HttpRequestException(
                $"The request to {request.RequestUri} was not sent because the password of proxy server '{proxyServer.Name}' can no longer be decrypted. Enter the password again on the proxy servers page."
            );
        }

        return base.SendAsync(request, cancellationToken);
    }
}
