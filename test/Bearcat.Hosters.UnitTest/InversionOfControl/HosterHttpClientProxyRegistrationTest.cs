using System.Net;
using Bearcat.Abstractions.Proxies;
using Bearcat.Hosters.InversionOfControl;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using FichierApiClient = Bearcat.Hosters.Fichier.Api.ApiClient;
using Keep2ShareApiClient = Bearcat.Hosters.Keep2Share.Api.ApiClient;

namespace Bearcat.Hosters.UnitTest.InversionOfControl;

public class HosterHttpClientProxyRegistrationTest
{
    private static readonly Uri Destination = new("https://hoster.test/file");
    private static readonly Uri UploadProxyUri = new("http://upload-proxy.test:8080");
    private static readonly Uri DownloadProxyUri = new("socks5://download-proxy.test:1080");

    private ServiceProvider serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        var proxyRoutingCacheMock = new Mock<IProxyRoutingCache>();
        proxyRoutingCacheMock
            .Setup(cache => cache.GetProxyServerForCategory(ProxyCategory.HosterUploads))
            .Returns(new ResolvedProxyServer(1, "Upload proxy", UploadProxyUri, null, false));
        proxyRoutingCacheMock
            .Setup(cache => cache.GetProxyServerForCategory(ProxyCategory.HosterMirrorDownloads))
            .Returns(new ResolvedProxyServer(2, "Download proxy", DownloadProxyUri, null, false));

        var services = new ServiceCollection();
        services.AddSingleton(proxyRoutingCacheMock.Object);
        services.AddHosters();
        serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        serviceProvider.Dispose();
    }

    [Test]
    public void UploadHttpClient_RoutesThroughHosterUploadsProxy()
    {
        // Act
        var webProxy = GetWebProxy(GetPrimaryHandler(HttpClientProvider.UploadHttpClientName));

        // Assert
        webProxy.GetProxy(Destination).ShouldBe(UploadProxyUri);
    }

    [Test]
    public void DownloadHttpClient_RoutesThroughHosterMirrorDownloadsProxy()
    {
        // Act
        var webProxy = GetWebProxy(GetPrimaryHandler(HttpClientProvider.DownloadHttpClientName));

        // Assert
        webProxy.GetProxy(Destination).ShouldBe(DownloadProxyUri);
    }

    [Test]
    public void FichierUploadHttpClient_KeepsDisabledRedirectsAndRoutesThroughHosterUploadsProxy()
    {
        // Act
        var primaryHandler = GetPrimaryHandler(FichierApiClient.UploadHttpClientName);

        // Assert
        var httpClientHandler = primaryHandler.ShouldBeOfType<HttpClientHandler>();
        httpClientHandler.AllowAutoRedirect.ShouldBeFalse();
        httpClientHandler.UseProxy.ShouldBeTrue();
        GetWebProxy(httpClientHandler).GetProxy(Destination).ShouldBe(UploadProxyUri);
    }

    [Test]
    public void Keep2ShareUploadHttpClient_KeepsCertificateValidationAndRoutesThroughHosterUploadsProxy()
    {
        // Act
        var primaryHandler = GetPrimaryHandler(Keep2ShareApiClient.UploadHttpClientName);

        // Assert
        var httpClientHandler = primaryHandler.ShouldBeOfType<HttpClientHandler>();
        httpClientHandler.ServerCertificateCustomValidationCallback.ShouldNotBeNull();
        httpClientHandler.UseProxy.ShouldBeTrue();
        GetWebProxy(httpClientHandler).GetProxy(Destination).ShouldBe(UploadProxyUri);
    }

    private HttpMessageHandler GetPrimaryHandler(string clientName)
    {
        var handler = serviceProvider
            .GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(clientName);

        while (handler is DelegatingHandler { InnerHandler: not null } delegatingHandler)
        {
            handler = delegatingHandler.InnerHandler;
        }

        return handler;
    }

    private static IWebProxy GetWebProxy(HttpMessageHandler primaryHandler)
    {
        return primaryHandler switch
        {
            SocketsHttpHandler socketsHttpHandler => socketsHttpHandler.Proxy!,
            HttpClientHandler httpClientHandler => httpClientHandler.Proxy!,
            _ => throw new InvalidOperationException(primaryHandler.GetType().Name),
        };
    }
}
