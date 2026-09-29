namespace Bearcat.Abstractions.Proxies;

public class UnreadableProxySecretsRejectingHandler(CategoryProxySelectingWebProxy webProxy)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var proxyServer = webProxy.SelectProxyServer();

        if (proxyServer is { HasUnreadableSecrets: true })
        {
            throw new HttpRequestException(
                $"The request to {request.RequestUri} was not sent because the password of proxy server '{proxyServer.Name}' can no longer be decrypted. Enter the password again on the proxy servers page."
            );
        }

        return base.SendAsync(request, cancellationToken);
    }
}
