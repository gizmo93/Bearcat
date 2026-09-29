using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Abstractions.Proxies;

public static class HttpClientBuilderProxyExtensions
{
    extension(IHttpClientBuilder builder)
    {
        public IHttpClientBuilder UseProxyForCategory(ProxyCategory category)
        {
            builder.ConfigurePrimaryHttpMessageHandler(
                (primaryHandler, serviceProvider) =>
                {
                    var webProxy = CreateWebProxy(category, serviceProvider);

                    switch (primaryHandler)
                    {
                        case SocketsHttpHandler socketsHttpHandler:
                            socketsHttpHandler.Proxy = webProxy;
                            socketsHttpHandler.UseProxy = true;
                            break;
                        case HttpClientHandler httpClientHandler:
                            httpClientHandler.Proxy = webProxy;
                            httpClientHandler.UseProxy = true;
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"The HTTP client '{builder.Name}' uses the primary handler {primaryHandler.GetType().Name}, which does not support a proxy. Use a SocketsHttpHandler or HttpClientHandler."
                            );
                    }
                }
            );

            builder.AddHttpMessageHandler(
                serviceProvider => new UnreadableProxySecretsRejectingHandler(
                    CreateWebProxy(category, serviceProvider)
                )
            );

            return builder;
        }
    }

    private static CategoryProxySelectingWebProxy CreateWebProxy(
        ProxyCategory category,
        IServiceProvider serviceProvider
    )
    {
        return new CategoryProxySelectingWebProxy(
            category,
            serviceProvider.GetRequiredService<IProxyRoutingCache>()
        );
    }
}
