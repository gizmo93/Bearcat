using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bearcat.Infrastructure.Updates;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.Updates;

public class GitHubUpdateCheckerTest
{
    private const string ReleasesPageUrl = "https://github.com/gizmo93/Bearcat/releases";
    private static readonly Uri LatestReleaseUri = new(
        "https://api.github.com/repos/gizmo93/Bearcat/releases/latest"
    );

    private TestHttpMessageHandler httpMessageHandler = null!;
    private GitHubUpdateChecker checker = null!;

    [SetUp]
    public void SetUp()
    {
        httpMessageHandler = new TestHttpMessageHandler();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(GitHubUpdateChecker.HttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        checker = new GitHubUpdateChecker(httpClientFactoryMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        checker.Dispose();
        httpMessageHandler.Dispose();
    }

    [Test]
    public async Task GetUpdateStatusAsync_NewerReleaseAvailable_ReturnsUpdateAvailable()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ =>
            JsonResponse(
                "{\"tag_name\":\"v9999.0.0\",\"html_url\":\"https://github.com/gizmo93/Bearcat/releases/tag/v9999.0.0\"}"
            )
        );

        // Act
        var status = await checker.GetUpdateStatusAsync(CancellationToken.None);

        // Assert
        status.CurrentVersion.ShouldBe(checker.CurrentVersion);
        status.LatestVersion.ShouldBe("9999.0.0");
        status.IsUpdateAvailable.ShouldBeTrue();
        status.ReleaseUrl.ShouldBe("https://github.com/gizmo93/Bearcat/releases/tag/v9999.0.0");
    }

    [Test]
    public async Task GetUpdateStatusAsync_OlderRelease_DoesNotReportUpdate()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ => JsonResponse("{\"tag_name\":\"0.0.1\"}"));

        // Act
        var status = await checker.GetUpdateStatusAsync(CancellationToken.None);

        // Assert
        status.LatestVersion.ShouldBe("0.0.1");
        status.IsUpdateAvailable.ShouldBeFalse();
    }

    [Test]
    public async Task GetUpdateStatusAsync_RequestsLatestReleaseEndpoint()
    {
        // Arrange
        Uri? requestedUri = null;
        httpMessageHandler.Enqueue(request =>
        {
            requestedUri = request.RequestUri;
            return JsonResponse("{\"tag_name\":\"9999.0.0\"}");
        });

        // Act
        await checker.GetUpdateStatusAsync(CancellationToken.None);

        // Assert
        requestedUri.ShouldBe(LatestReleaseUri);
    }

    [Test]
    public async Task GetUpdateStatusAsync_MissingHtmlUrl_FallsBackToReleasesPage()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ => JsonResponse("{\"tag_name\":\"9999.0.0\"}"));

        // Act
        var status = await checker.GetUpdateStatusAsync(CancellationToken.None);

        // Assert
        status.ReleaseUrl.ShouldBe(ReleasesPageUrl);
    }

    [Test]
    public async Task GetUpdateStatusAsync_EmptyTagName_ReturnsNoUpdate()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ => JsonResponse("{\"tag_name\":\"\"}"));

        // Act
        var status = await checker.GetUpdateStatusAsync(CancellationToken.None);

        // Assert
        status.LatestVersion.ShouldBeNull();
        status.IsUpdateAvailable.ShouldBeFalse();
        status.ReleaseUrl.ShouldBe(ReleasesPageUrl);
    }

    [Test]
    public async Task GetUpdateStatusAsync_HttpRequestFails_ReturnsNoUpdate()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ => throw new HttpRequestException("network down"));

        // Act
        var status = await checker.GetUpdateStatusAsync(CancellationToken.None);

        // Assert
        status.LatestVersion.ShouldBeNull();
        status.IsUpdateAvailable.ShouldBeFalse();
        status.ReleaseUrl.ShouldBe(ReleasesPageUrl);
    }

    [Test]
    public async Task GetUpdateStatusAsync_MalformedJson_ReturnsNoUpdate()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ => JsonResponse("this is not json"));

        // Act
        var status = await checker.GetUpdateStatusAsync(CancellationToken.None);

        // Assert
        status.LatestVersion.ShouldBeNull();
        status.IsUpdateAvailable.ShouldBeFalse();
    }

    [Test]
    public async Task GetUpdateStatusAsync_CalledTwice_CachesFirstResult()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ => JsonResponse("{\"tag_name\":\"9999.0.0\"}"));

        // Act
        var first = await checker.GetUpdateStatusAsync(CancellationToken.None);
        var second = await checker.GetUpdateStatusAsync(CancellationToken.None);

        // Assert
        second.ShouldBe(first);
        httpMessageHandler.RequestCount.ShouldBe(1);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
            },
        };
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = [];

        public int RequestCount { get; private set; }

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response)
        {
            responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;
            responses.Count.ShouldBeGreaterThan(0);
            return Task.FromResult(responses.Dequeue()(request));
        }
    }
}
