using System.Net;
using System.Net.Sockets;
using System.Text;
using Bearcat.Abstractions.Proxies;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Proxies;

public class HttpClientBuilderProxyExtensionsTest
{
    private const string ClientName = "TestClient";

    private FakeProxyRoutingCache proxyRoutingCache = null!;
    private ServiceCollection services = null!;

    [SetUp]
    public void SetUp()
    {
        proxyRoutingCache = new FakeProxyRoutingCache();
        services = new ServiceCollection();
        services.AddSingleton<IProxyRoutingCache>(proxyRoutingCache);
    }

    [Test]
    public void UseProxyForCategory_DefaultPrimaryHandler_SetsCategoryProxy()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(ProxyCategory.NfoDatabases, "http://nfo-proxy.test:8080");
        services.AddHttpClient(ClientName).UseProxyForCategory(ProxyCategory.NfoDatabases);

        // Act
        var handlers = CreateHandlerChain();

        // Assert
        var webProxy = GetConfiguredWebProxy(handlers[^1]);
        webProxy
            .GetProxy(new Uri("https://nfo.test"))
            .ShouldBe(new Uri("http://nfo-proxy.test:8080"));
    }

    [Test]
    public void UseProxyForCategory_CustomSocketsHttpHandlerRegisteredBefore_KeepsHandlerSettings()
    {
        // Arrange
        services
            .AddHttpClient(ClientName)
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { AllowAutoRedirect = false }
            )
            .UseProxyForCategory(ProxyCategory.MediaDatabases);

        // Act
        var handlers = CreateHandlerChain();

        // Assert
        var socketsHttpHandler = handlers[^1].ShouldBeOfType<SocketsHttpHandler>();
        socketsHttpHandler.AllowAutoRedirect.ShouldBeFalse();
        socketsHttpHandler.UseProxy.ShouldBeTrue();
        socketsHttpHandler.Proxy.ShouldBeOfType<CategoryProxySelectingWebProxy>();
    }

    [Test]
    public void UseProxyForCategory_CustomHttpClientHandlerRegisteredBefore_KeepsHandlerSettings()
    {
        // Arrange
        services
            .AddHttpClient(ClientName)
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            )
            .UseProxyForCategory(ProxyCategory.HosterUploads);

        // Act
        var handlers = CreateHandlerChain();

        // Assert
        var httpClientHandler = handlers[^1].ShouldBeOfType<HttpClientHandler>();
        httpClientHandler.AllowAutoRedirect.ShouldBeFalse();
        httpClientHandler.UseProxy.ShouldBeTrue();
        httpClientHandler.Proxy.ShouldBeOfType<CategoryProxySelectingWebProxy>();
    }

    [Test]
    public void UseProxyForCategory_UnsupportedPrimaryHandler_ThrowsWhenHandlerIsCreated()
    {
        // Arrange
        services
            .AddHttpClient(ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new UnsupportedHandler())
            .UseProxyForCategory(ProxyCategory.LinkCrypters);

        // Act
        var act = () => CreateHandlerChain();

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain(ClientName);
    }

    [Test]
    public void UseProxyForCategory_Always_AddsUnreadableProxySecretsRejectingHandler()
    {
        // Arrange
        services.AddHttpClient(ClientName).UseProxyForCategory(ProxyCategory.ImageHosters);

        // Act
        var handlers = CreateHandlerChain();

        // Assert
        handlers.OfType<UnreadableProxySecretsRejectingHandler>().ShouldHaveSingleItem();
    }

    [Test]
    public async Task SendAsync_CategoryRoutedThroughHttpProxy_SendsRequestToProxy()
    {
        // Arrange
        using var fakeProxy = new TcpListener(IPAddress.Loopback, 0);
        fakeProxy.Start();
        var proxyPort = ((IPEndPoint)fakeProxy.LocalEndpoint).Port;
        var receivedRequestLine = AnswerFirstRequestAsync(fakeProxy);
        proxyRoutingCache.RouteCategory(
            ProxyCategory.ImageHosters,
            $"http://127.0.0.1:{proxyPort}"
        );
        services.AddHttpClient(ClientName).UseProxyForCategory(ProxyCategory.ImageHosters);
        using var serviceProvider = services.BuildServiceProvider();
        using var httpClient = serviceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(ClientName);

        // Act
        var responseBody = await httpClient.GetStringAsync("http://images.invalid/upload");

        // Assert
        responseBody.ShouldBe("proxied");
        (await receivedRequestLine).ShouldBe("GET http://images.invalid/upload HTTP/1.1");
    }

    private List<HttpMessageHandler> CreateHandlerChain()
    {
        using var serviceProvider = services.BuildServiceProvider();
        var handler = serviceProvider
            .GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(ClientName);
        var handlers = new List<HttpMessageHandler> { handler };

        while (handlers[^1] is DelegatingHandler { InnerHandler: not null } delegatingHandler)
        {
            handlers.Add(delegatingHandler.InnerHandler);
        }

        return handlers;
    }

    private static IWebProxy GetConfiguredWebProxy(HttpMessageHandler primaryHandler)
    {
        return primaryHandler switch
        {
            SocketsHttpHandler socketsHttpHandler => socketsHttpHandler.Proxy!,
            HttpClientHandler httpClientHandler => httpClientHandler.Proxy!,
            _ => throw new InvalidOperationException(primaryHandler.GetType().Name),
        };
    }

    private static async Task<string> AnswerFirstRequestAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        var buffer = new byte[4096];
        var request = new StringBuilder();

        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var readByteCount = await stream.ReadAsync(buffer);
            request.Append(Encoding.ASCII.GetString(buffer, 0, readByteCount));
        }

        await stream.WriteAsync(
            "HTTP/1.1 200 OK\r\nContent-Length: 7\r\nConnection: close\r\n\r\nproxied"u8.ToArray()
        );

        return request.ToString().Split("\r\n")[0];
    }

    private sealed class UnsupportedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
