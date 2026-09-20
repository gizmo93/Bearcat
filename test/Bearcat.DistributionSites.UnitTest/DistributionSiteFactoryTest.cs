using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.DistributionSites.InversionOfControl;
using Bearcat.DistributionSites.Shared.XenForo.Api;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest;

public class DistributionSiteFactoryTest
{
    [Test]
    public async Task Create_TwoGenericForumsInOneScope_KeepTheirOwnUrlsAndSessions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributionSites();

        var handler = new RecordingHandler();
        services
            .AddHttpClient(XenForoForumClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDistributionSiteFactory>();

        var firstSession = new DistributionSession(
            "Bearcat",
            [
                new SessionCookie(
                    Name: "xf_user",
                    Value: "first",
                    Domain: "first.example",
                    Path: "/community/"
                ),
            ]
        );
        var secondSession = new DistributionSession(
            "Bearcat",
            [
                new SessionCookie(
                    Name: "xf_user",
                    Value: "second",
                    Domain: "second.example",
                    Path: "/"
                ),
            ]
        );

        // Act
        var first = (IForumDistributionSite)
            factory.Create(
                "XenForo",
                """{"BaseUrl":"https://first.example/community","Username":"first","Password":"secret"}"""
            );
        var second = (IForumDistributionSite)
            factory.Create(
                "XenForo",
                """{"BaseUrl":"https://second.example/","Username":"second","Password":"secret"}"""
            );

        var firstSessionValid = await first.IsSessionValidAsync(
            firstSession,
            CancellationToken.None
        );
        var secondSessionValid = await second.IsSessionValidAsync(
            secondSession,
            CancellationToken.None
        );
        var firstSessionStillValid = await first.IsSessionValidAsync(
            firstSession,
            CancellationToken.None
        );
        var firstTarget = first.ResolveTarget("12");
        var secondTarget = second.ResolveTarget("12");
        var genericSite = factory
            .GetDistributionSites()
            .Single(site => site.ClassName == "XenForo");

        // Assert
        firstSessionValid.ShouldBeTrue();
        secondSessionValid.ShouldBeTrue();
        firstSessionStillValid.ShouldBeTrue();
        firstTarget.Value.ShouldBe("https://first.example/community/forums/12/");
        secondTarget.Value.ShouldBe("https://second.example/forums/12/");
        handler.Requests.ShouldBe([
            ("https://first.example/community/", "xf_user=first"),
            ("https://second.example/", "xf_user=second"),
            ("https://first.example/community/", "xf_user=first"),
        ]);

        genericSite
            .ConfigurationFields.Select(field => field.Key)
            .ShouldBe(["BaseUrl", "Username", "Password"]);
    }

    [TestCase("DataLoadMe", "https://www.data-load.me/")]
    [TestCase("BoerseCx", "https://boerse.cx/")]
    public void Create_ExplicitForum_KeepsItsFixedUrl(string className, string expectedUrl)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributionSites();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDistributionSiteFactory>();

        // Act
        var site = factory.Create(className, """{"Username":"user","Password":"secret"}""");

        // Assert
        site.BaseUrl.ShouldBe(expectedUrl);
        site.ConfigurationFields.Select(field => field.Key).ShouldBe(["Username", "Password"]);
    }

    [TestCase("https://example.org/community", "https://example.org/community/")]
    [TestCase("  https://example.org/  ", "https://example.org/")]
    [TestCase("http://localhost:8080/forum/", "http://localhost:8080/forum/")]
    public void SerializeConfig_ValidUrl_NormalizesAndPreservesCredentials(
        string baseUrl,
        string expectedUrl
    )
    {
        // Arrange
        var site = new XenForo.XenForo(Moq.Mock.Of<IHttpClientFactory>());
        var configuration = new Dictionary<string, string>
        {
            ["BaseUrl"] = baseUrl,
            ["Username"] = "user",
            ["Password"] = " secret ",
        };

        // Act
        var serialized = site.SerializeConfig(configuration);

        // Assert
        var config = JsonSerializer.Deserialize<XenForo.XenForoConfig>(serialized)!;
        config.BaseUrl.ShouldBe(expectedUrl);
        config.Username.ShouldBe("user");
        config.Password.ShouldBe(" secret ");
        config.ToDictionary()["BaseUrl"].ShouldBe(expectedUrl);
    }

    [TestCase("")]
    [TestCase("example.org")]
    [TestCase("file:///tmp/forum")]
    [TestCase("https://user:password@example.org/")]
    [TestCase("https://example.org/index.php?forums/")]
    [TestCase("https://example.org/index.php")]
    [TestCase("https://example.org/#forum")]
    public void SerializeConfig_InvalidUrl_RejectsConfiguration(string baseUrl)
    {
        // Arrange
        var site = new XenForo.XenForo(Moq.Mock.Of<IHttpClientFactory>());

        var configuration = new Dictionary<string, string>
        {
            ["BaseUrl"] = baseUrl,
            ["Username"] = "user",
            ["Password"] = "secret",
        };

        // Act & Assert
        Should.Throw<ValidationException>(() => site.SerializeConfig(configuration));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Url, string Cookie)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(
                (
                    request.RequestUri!.AbsoluteUri,
                    string.Join("; ", request.Headers.GetValues("Cookie"))
                )
            );

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html data-logged-in=\"true\"></html>"),
                }
            );
        }
    }
}
