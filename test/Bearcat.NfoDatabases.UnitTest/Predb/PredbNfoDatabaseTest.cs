using System.Net;
using System.Text;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Predb;
using Bearcat.NfoDatabases.Predb.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Refit;
using Shouldly;

namespace Bearcat.NfoDatabases.UnitTest.Predb;

public class PredbNfoDatabaseTest
{
    private Mock<IPredbApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private PredbNfoDatabase service = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IPredbApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        service = new PredbNfoDatabase(
            new PredbClient(
                apiMock.Object,
                new PredbRateLimiter(),
                new PredbDownloadQuota(),
                httpClientFactoryMock.Object,
                NullLogger<PredbClient>.Instance
            )
        );
    }

    [Test]
    public async Task GetReleaseInfoAsync_ReleaseFound_MapsRelease()
    {
        // Arrange
        apiMock
            .Setup(api => api.GetPreAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new PredbPreResponse(
                        Status: "success",
                        Message: "",
                        Results: 1,
                        Data:
                        [
                            new PredbReleaseResponse(
                                Release: "Movie.Release.2026-GRP",
                                Section: "X264",
                                Size: 4124.6,
                                Group: "GRP",
                                Genre: "sci-fi"
                            ),
                        ]
                    )
                )
            );

        // Act
        var result = await service.GetReleaseInfoAsync(
            new PredbConfig(),
            "Movie Release 2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ReleaseName.ShouldBe("Movie.Release.2026-GRP");
        result.ReleaseDatabaseUrl.ShouldBe("https://predb.net/rls/Movie.Release.2026-GRP");
        result.Size.ShouldBe(new ReleaseInfoSize(4125, "MB"));
        result.Genre.ShouldBe("sci-fi");
        result.ExternalInfos.ShouldBeEmpty();
    }

    [Test]
    public async Task GetReleaseInfoAsync_NoResults_ReturnsNull()
    {
        // Arrange
        apiMock
            .Setup(api => api.GetPreAsync("Unknown.Release-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new PredbPreResponse(
                        Status: "success",
                        Message: "No results found.",
                        Results: 0,
                        Data: null
                    )
                )
            );

        // Act
        var result = await service.GetReleaseInfoAsync(
            new PredbConfig(),
            "Unknown.Release-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetReleaseInfoAsync_SizeIsZero_ReturnsNullSize()
    {
        // Arrange
        apiMock
            .Setup(api => api.GetPreAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new PredbPreResponse(
                        Status: "success",
                        Message: "",
                        Results: 1,
                        Data:
                        [
                            new PredbReleaseResponse(
                                Release: "Movie.Release.2026-GRP",
                                Section: "X264",
                                Size: 0,
                                Group: "GRP",
                                Genre: ""
                            ),
                        ]
                    )
                )
            );

        // Act
        var result = await service.GetReleaseInfoAsync(
            new PredbConfig(),
            "Movie.Release.2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.Size.ShouldBeNull();
        result.Genre.ShouldBeNull();
    }

    [Test]
    public async Task GetReleaseNfoAsync_NfoFound_DownloadsText()
    {
        // Arrange
        const string nfoUrl = "https://nfo.predb.net/t/Movie.Release.2026-GRP.nfo";
        apiMock
            .Setup(api => api.GetNfoAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new PredbNfoResponse(
                        Status: "success",
                        Message: "",
                        Results: 2,
                        Data: new PredbNfoData(
                            Nfo: nfoUrl,
                            NfoImage: "https://api.predb.net/nfoimg/Movie.Release.2026-GRP.png"
                        )
                    )
                )
            );
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(PredbClient.DownloadHttpClientName))
            .Returns(CreateHttpClient("remote nfo content"));

        // Act
        var result = await service.GetReleaseNfoAsync(
            new PredbConfig(),
            "Movie Release 2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.FileName.ShouldBe("Movie.Release.2026-GRP.nfo");
        result.Content.ShouldBe("remote nfo content");
    }

    [Test]
    public async Task GetReleaseNfoAsync_NfoUrlEmpty_ReturnsNull()
    {
        // Arrange
        apiMock
            .Setup(api => api.GetNfoAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new PredbNfoResponse(
                        Status: "success",
                        Message: "No results found.",
                        Results: 0,
                        Data: null
                    )
                )
            );

        // Act
        var result = await service.GetReleaseNfoAsync(
            new PredbConfig(),
            "Movie.Release.2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetReleaseNfoAsync_DownloadLimitReached_ReturnsNullAndStopsFurtherAttempts()
    {
        // Arrange
        apiMock
            .Setup(api => api.GetNfoAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new PredbNfoResponse(
                        Status: "success",
                        Message: "",
                        Results: 2,
                        Data: new PredbNfoData(
                            Nfo: "https://nfo.predb.net/t/Movie.Release.2026-GRP.nfo",
                            NfoImage: null
                        )
                    )
                )
            );
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(PredbClient.DownloadHttpClientName))
            .Returns(CreateHttpClient("You've reached your daily download limit."));

        // Act
        var first = await service.GetReleaseNfoAsync(
            new PredbConfig(),
            "Movie.Release.2026-GRP",
            CancellationToken.None
        );
        var second = await service.GetReleaseNfoAsync(
            new PredbConfig(),
            "Other.Release.2026-GRP",
            CancellationToken.None
        );

        // Assert
        first.ShouldBeNull();
        second.ShouldBeNull();
        apiMock.Verify(
            api => api.GetNfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        httpClientFactoryMock.Verify(
            factory => factory.CreateClient(PredbClient.DownloadHttpClientName),
            Times.Once
        );
    }

    [Test]
    public async Task GetReleaseNfoAsync_LongNfoMentioningTheLimitPhrase_IsNotTreatedAsLimit()
    {
        // Arrange
        var nfoContent = new string('x', 300) + " daily download limit";
        apiMock
            .Setup(api => api.GetNfoAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new PredbNfoResponse(
                        Status: "success",
                        Message: "",
                        Results: 2,
                        Data: new PredbNfoData(
                            Nfo: "https://nfo.predb.net/t/Movie.Release.2026-GRP.nfo",
                            NfoImage: null
                        )
                    )
                )
            );
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(PredbClient.DownloadHttpClientName))
            .Returns(CreateHttpClient(nfoContent));

        // Act
        var result = await service.GetReleaseNfoAsync(
            new PredbConfig(),
            "Movie.Release.2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.Content.ShouldBe(nfoContent);
    }

    private static HttpClient CreateHttpClient(string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes(content)),
                }
            );

        return new HttpClient(handlerMock.Object);
    }

    private static ApiResponse<T> CreateApiResponse<T>(
        HttpStatusCode statusCode,
        T? content = default
    )
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            content!,
            new RefitSettings(),
            error: null
        );
    }
}
