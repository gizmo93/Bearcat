using System.Net;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Srrdb;
using Bearcat.NfoDatabases.Srrdb.Api;
using Moq;
using Moq.Protected;
using Refit;
using Shouldly;

namespace Bearcat.NfoDatabases.UnitTest.Srrdb;

public class SrrdbNfoDatabaseTest
{
    private Mock<ISrrdbApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private SrrdbNfoDatabase service = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<ISrrdbApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        service = new SrrdbNfoDatabase(
            new SrrdbClient(apiMock.Object, httpClientFactoryMock.Object)
        );
    }

    [Test]
    public async Task GetReleaseInfoAsync_ReleaseFound_MapsDetailsAndImdb()
    {
        // Arrange
        apiMock
            .Setup(api =>
                api.GetDetailsAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new SrrdbDetailsResponse(
                        Name: "Movie.Release.2026-GRP",
                        Files: [],
                        ArchivedFiles:
                        [
                            new SrrdbFileResponse("movie.mkv", 1024 * 1024 * 700L, "DEADBEEF"),
                        ]
                    )
                )
            );
        apiMock
            .Setup(api => api.GetImdbAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new SrrdbImdbResponse(
                        Releases:
                        [
                            new SrrdbImdbReleaseResponse(
                                Imdb: "1234567",
                                Title: "Movie Release",
                                Rating: "7.1",
                                Votes: "42"
                            ),
                        ],
                        Query: "Movie.Release.2026-GRP"
                    )
                )
            );

        // Act
        var result = await service.GetReleaseInfoAsync(
            new SrrdbConfig(),
            "Movie Release 2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ReleaseName.ShouldBe("Movie.Release.2026-GRP");
        result.ReleaseDatabaseUrl.ShouldBe(
            "https://www.srrdb.com/release/details/Movie.Release.2026-GRP"
        );
        result.Size.ShouldBe(new ReleaseInfoSize(700, "MB"));
        result.ExternalInfos.Single().Title.ShouldBe("Movie Release");
        result
            .ExternalInfos.Single()
            .Urls.Single()
            .Value.ShouldBe("https://www.imdb.com/title/tt1234567");
    }

    [Test]
    public async Task GetReleaseNfoAsync_NfoFound_DownloadsText()
    {
        // Arrange
        const string nfoUrl =
            "https://www.srrdb.com/download/file/Movie.Release.2026-GRP/movie.nfo";
        apiMock
            .Setup(api => api.GetNfoAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new SrrdbNfoResponse(
                        Release: "Movie.Release.2026-GRP",
                        Nfo: ["movie.nfo"],
                        NfoLink: [nfoUrl]
                    )
                )
            );
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient("SrrdbNfoDownload"))
            .Returns(CreateHttpClient("remote nfo content"));

        // Act
        var result = await service.GetReleaseNfoAsync(
            new SrrdbConfig(),
            "Movie Release 2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.FileName.ShouldBe("movie.nfo");
        result.Content.ShouldBe("remote nfo content");
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
                    Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content)),
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
            new HttpResponseMessage(statusCode),
            content!,
            new RefitSettings(),
            error: null
        );
    }
}
