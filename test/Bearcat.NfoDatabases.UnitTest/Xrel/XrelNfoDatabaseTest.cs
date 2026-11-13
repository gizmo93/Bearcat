using System.Globalization;
using System.Net;
using System.Text;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Xrel;
using Bearcat.NfoDatabases.Xrel.Api;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.NfoDatabases.UnitTest.Xrel;

public class XrelNfoDatabaseTest
{
    private Mock<IXrelApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private XrelNfoDatabase service = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IXrelApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(XrelNfoDatabase.CoverHttpClientName))
            .Returns(() => CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        service = new XrelNfoDatabase(
            new XrelClient(apiMock.Object, new XrelRateLimitState(), httpClientFactoryMock.Object),
            httpClientFactoryMock.Object
        );
    }

    [Test]
    public async Task GetReleaseInfoAsync_SceneReleaseFound_MapsReleaseAndDoesNotCallP2p()
    {
        // Arrange
        apiMock
            .Setup(api =>
                api.GetReleaseInfoAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new XrelRelease(
                        Dirname: "Movie.Release.2026-GRP",
                        LinkHref: "/release/123/Movie-Release.html",
                        Size: new XrelReleaseSize(42, "GB"),
                        VideoType: "WEB",
                        AudioType: "AC3",
                        ExtInfo: new XrelExternalInfo(
                            Type: "movie",
                            Id: "movie123",
                            Title: "Movie Release",
                            LinkHref: "//www.xrel.to/movie/123/Movie-Release.html",
                            Uris: ["imdb:tt1234567", "https://metadata.test/movie"]
                        )
                    )
                )
            );
        apiMock
            .Setup(api =>
                api.GetExternalInfoDetailsAsync("movie123", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new XrelExternalInfoDetails(
                        Type: "movie",
                        Id: "movie123",
                        Title: "Movie Release",
                        LinkHref: "https://www.xrel.to/movie/123/Movie-Release.html",
                        Genre: "Drama, Sci-Fi",
                        CoverUrl: "https://uploads2.xrel.to/img_cover/movie123.JPG",
                        Uris: ["imdb:tt1234567"],
                        Externals:
                        [
                            new XrelExternalInfoExternal(
                                Plot: "Movie &quot;plot&quot;<br />\n<br />\nSecond line",
                                LinkUrl: null,
                                Source: null
                            ),
                        ]
                    )
                )
            );
        apiMock
            .Setup(api => api.GetExternalInfoMediaAsync("movie123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse<IReadOnlyList<XrelExternalInfoMedia>>(
                    HttpStatusCode.OK,
                    [
                        new XrelExternalInfoMedia(
                            Type: "video",
                            Description: "Trailer",
                            UrlFull: null,
                            UrlThumb: "https://uploads2.xrel.to/img_mediathek_thumb/video.JPG"
                        ),
                        new XrelExternalInfoMedia(
                            Type: "image",
                            Description: "Poster",
                            UrlFull: "//uploads2.xrel.to/img_mediathek/movie123.JPG",
                            UrlThumb: "https://uploads2.xrel.to/img_mediathek_thumb/movie123.JPG"
                        ),
                    ]
                )
            );

        // Act
        var result = await service.GetReleaseInfoAsync(
            new XrelConfig(),
            "Movie  Release 2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ReleaseName.ShouldBe("Movie.Release.2026-GRP");
        result.ReleaseDatabaseUrl.ShouldBe("https://www.xrel.to/release/123/Movie-Release.html");
        result.Size.ShouldBe(new ReleaseInfoSize(42, "GB"));
        result.VideoType.ShouldBe("WEB");
        result.AudioType.ShouldBe("AC3");
        result.Genre.ShouldBe("Drama, Sci-Fi");
        result.Description.ShouldBe("Movie \"plot\"\n\nSecond line");
        result.CoverUrl.ShouldBe("https://uploads2.xrel.to/img_cover/movie123-full.JPG");

        var externalInfo = result.ExternalInfos.Single();
        externalInfo.Type.ShouldBe(ExternalInfoType.Movie);
        externalInfo.Title.ShouldBe("Movie Release");
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Other
            && url.Value == "https://www.xrel.to/movie/123/Movie-Release.html"
        );
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Imdb && url.Value == "https://www.imdb.com/de/title/tt1234567"
        );
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Other && url.Value == "https://metadata.test/movie"
        );
        apiMock.Verify(
            api => api.GetP2pReleaseInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        apiMock.Verify(
            api => api.GetExternalInfoDetailsAsync("movie123", It.IsAny<CancellationToken>()),
            Times.Once
        );
        apiMock.Verify(
            api => api.GetExternalInfoMediaAsync("movie123", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task GetReleaseInfoAsync_SceneReleaseMissing_P2pReleaseFound_MapsP2pRelease()
    {
        // Arrange
        apiMock
            .Setup(api =>
                api.GetReleaseInfoAsync("P2P.Release.2026-GRP", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse<XrelRelease>(HttpStatusCode.NotFound));
        apiMock
            .Setup(api =>
                api.GetP2pReleaseInfoAsync("P2P.Release.2026-GRP", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new XrelP2PRelease(
                        Dirname: "P2P.Release.2026-GRP",
                        LinkHref: "https://www.xrel.to/p2p/42-P2P-Release/nfo.html",
                        SizeMb: 11900,
                        ExtInfo: new XrelExternalInfo(
                            Type: "movie",
                            Id: "movie42",
                            Title: "P2P Release",
                            LinkHref: "https://www.xrel.to/movie/42/P2P-Release.html",
                            Uris: ["imdb:tt7654321"]
                        )
                    )
                )
            );
        apiMock
            .Setup(api => api.GetExternalInfoDetailsAsync("movie42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new XrelExternalInfoDetails(
                        Type: "movie",
                        Id: "movie42",
                        Title: "P2P Release",
                        LinkHref: "https://www.xrel.to/movie/42/P2P-Release.html",
                        Genre: "Action",
                        CoverUrl: "https://uploads2.xrel.to/img_cover/movie42.JPG",
                        Uris: ["imdb:tt7654321"],
                        Externals: []
                    )
                )
            );
        apiMock
            .Setup(api => api.GetExternalInfoMediaAsync("movie42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse<IReadOnlyList<XrelExternalInfoMedia>>(
                    HttpStatusCode.OK,
                    [
                        new XrelExternalInfoMedia(
                            Type: "image",
                            Description: "Poster",
                            UrlFull: "https://uploads2.xrel.to/img_mediathek/movie42.JPG",
                            UrlThumb: "https://uploads2.xrel.to/img_mediathek_thumb/movie42.JPG"
                        ),
                    ]
                )
            );

        // Act
        var result = await service.GetReleaseInfoAsync(
            new XrelConfig(),
            "P2P Release 2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.ReleaseName.ShouldBe("P2P.Release.2026-GRP");
        result.ReleaseDatabaseUrl.ShouldBe("https://www.xrel.to/p2p/42-P2P-Release/nfo.html");
        result.Size.ShouldBe(new ReleaseInfoSize(11900, "MB"));
        result.VideoType.ShouldBeNull();
        result.AudioType.ShouldBeNull();
        result.Genre.ShouldBe("Action");
        result.CoverUrl.ShouldBe("https://uploads2.xrel.to/img_cover/movie42-full.JPG");
        result
            .ExternalInfos.Single()
            .Urls.ShouldContain(url =>
                url.Type == UrlType.Imdb && url.Value == "https://www.imdb.com/de/title/tt7654321"
            );
    }

    [Test]
    public async Task GetReleaseInfoAsync_BothEndpointsMiss_ReturnsNull()
    {
        // Arrange
        apiMock
            .Setup(api => api.GetReleaseInfoAsync("Unknown.Release", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApiResponse<XrelRelease>(HttpStatusCode.NotFound));
        apiMock
            .Setup(api =>
                api.GetP2pReleaseInfoAsync("Unknown.Release", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse<XrelP2PRelease>(HttpStatusCode.NotFound));

        // Act
        var result = await service.GetReleaseInfoAsync(
            new XrelConfig(),
            "Unknown Release",
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetReleaseInfoAsync_DecomposedUmlautsInName_QueriesOnlyP2pWithComposedName()
    {
        // Arrange
        const string composedName = "Türkische.Früchte.1973.German.1080p.Web-DL.x264-MMS";
        apiMock
            .Setup(api => api.GetP2pReleaseInfoAsync(composedName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApiResponse<XrelP2PRelease>(HttpStatusCode.NotFound));

        // Act
        var result = await service.GetReleaseInfoAsync(
            new XrelConfig(),
            composedName.Normalize(NormalizationForm.FormD),
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
        apiMock.Verify(
            api => api.GetP2pReleaseInfoAsync(composedName, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task GetReleaseInfoAsync_RateLimitReached_ThrowsRateLimitException()
    {
        // Arrange
        var resetAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        apiMock
            .Setup(api => api.GetReleaseInfoAsync("Limited.Release", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse<XrelRelease>(
                    HttpStatusCode.TooManyRequests,
                    headers: new Dictionary<string, string>
                    {
                        ["X-RateLimit-Remaining"] = "0",
                        ["X-RateLimit-Reset"] = resetAt.ToString(CultureInfo.InvariantCulture),
                    }
                )
            );

        // Act
        var exception = await Should.ThrowAsync<NfoDatabaseRateLimitExceededException>(() =>
            service.GetReleaseInfoAsync(new XrelConfig(), "Limited Release", CancellationToken.None)
        );

        // Assert
        exception.DatabaseName.ShouldBe("xREL");
        exception.ResetAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(resetAt));
        apiMock.Verify(
            api => api.GetP2pReleaseInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task GetReleaseInfoAsync_FullCoverUrlMissing_UsesMediaCoverUrl()
    {
        // Arrange
        var checkedCoverUrls = new List<string>();
        httpClientFactoryMock.Reset();
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(XrelNfoDatabase.CoverHttpClientName))
            .Returns(() =>
                CreateHttpClient(request =>
                {
                    checkedCoverUrls.Add(request.RequestUri!.ToString());
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                })
            );
        apiMock
            .Setup(api =>
                api.GetReleaseInfoAsync("Movie.Release.2026-GRP", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new XrelRelease(
                        Dirname: "Movie.Release.2026-GRP",
                        LinkHref: "/release/123/Movie-Release.html",
                        Size: new XrelReleaseSize(42, "GB"),
                        VideoType: "WEB",
                        AudioType: "AC3",
                        ExtInfo: new XrelExternalInfo(
                            Type: "movie",
                            Id: "movie123",
                            Title: "Movie Release",
                            LinkHref: "//www.xrel.to/movie/123/Movie-Release.html",
                            Uris: []
                        )
                    )
                )
            );
        apiMock
            .Setup(api =>
                api.GetExternalInfoDetailsAsync("movie123", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new XrelExternalInfoDetails(
                        Type: "movie",
                        Id: "movie123",
                        Title: "Movie Release",
                        LinkHref: "https://www.xrel.to/movie/123/Movie-Release.html",
                        Genre: "Drama",
                        CoverUrl: "https://uploads2.xrel.to/img_cover/movie123.JPG",
                        Uris: [],
                        Externals: []
                    )
                )
            );
        apiMock
            .Setup(api => api.GetExternalInfoMediaAsync("movie123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse<IReadOnlyList<XrelExternalInfoMedia>>(
                    HttpStatusCode.OK,
                    [
                        new XrelExternalInfoMedia(
                            Type: "image",
                            Description: "Poster",
                            UrlFull: "//uploads2.xrel.to/img_mediathek/movie123.JPG",
                            UrlThumb: "https://uploads2.xrel.to/img_mediathek_thumb/movie123.JPG"
                        ),
                    ]
                )
            );

        // Act
        var result = await service.GetReleaseInfoAsync(
            new XrelConfig(),
            "Movie Release 2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.CoverUrl.ShouldBe("https://uploads2.xrel.to/img_mediathek/movie123.JPG");
        checkedCoverUrls.ShouldBe(["https://uploads2.xrel.to/img_cover/movie123-full.JPG"]);
    }

    [Test]
    public async Task GetReleaseNfoAsync_P2pNfoPageContainsStrippedNfo_ReturnsDecodedAndNormalizedNfo()
    {
        // Arrange
        SetupP2pRelease("P2P.Release.2026-GRP");
        var requestedUrls = new List<string>();

        SetupNfoPage(request =>
        {
            requestedUrls.Add(request.RequestUri!.ToString());

            return CreateHtmlResponse(
                HttpStatusCode.OK,
                """
                <html><head><title>P2P.Release.2026-GRP NFO</title></head><body>
                <div id="nfo_stripped" style="display: none;"> <pre>

                   P2P &quot;Release&quot;&#32;&#32;
                Group&#039;s finest



                Web: <a href="https://group.test" target="_blank">https://group.test</a>
                </pre> </div>
                </body></html>
                """
            );
        });

        // Act
        var result = await service.GetReleaseNfoAsync(
            new XrelConfig(),
            "P2P Release 2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.FileName.ShouldBe("P2P.Release.2026-GRP.nfo");
        result.Content.ShouldBe("   P2P \"Release\"\nGroup's finest\n\nWeb: https://group.test");
        requestedUrls.ShouldBe(["https://www.xrel.to/p2p/42-P2P-Release/nfo.html"]);
    }

    [Test]
    public async Task GetReleaseNfoAsync_NfoPageWithoutStrippedNfo_ReturnsNull()
    {
        // Arrange
        SetupP2pRelease("P2P.Release.2026-GRP");

        SetupNfoPage(_ =>
            CreateHtmlResponse(
                HttpStatusCode.OK,
                "<html><head><title>NFO</title></head><body><div id=\"nfo\"></div></body></html>"
            )
        );

        // Act
        var result = await service.GetReleaseNfoAsync(
            new XrelConfig(),
            "P2P.Release.2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetReleaseNfoAsync_CloudflareChallenge_ThrowsRateLimitExceeded()
    {
        // Arrange
        SetupP2pRelease("P2P.Release.2026-GRP");

        SetupNfoPage(_ =>
            CreateHtmlResponse(
                HttpStatusCode.TooManyRequests,
                "<html><head><title>Just a moment...</title></head><body></body></html>"
            )
        );

        // Act
        var action = () =>
            service.GetReleaseNfoAsync(
                new XrelConfig(),
                "P2P.Release.2026-GRP",
                CancellationToken.None
            );

        // Assert
        var exception = await action.ShouldThrowAsync<NfoDatabaseRateLimitExceededException>();
        exception.DatabaseName.ShouldBe("xREL");
    }

    [Test]
    public async Task GetReleaseNfoAsync_P2pReleaseNotFound_ReturnsNullWithoutNfoPageRequest()
    {
        // Arrange
        apiMock
            .Setup(api =>
                api.GetP2pReleaseInfoAsync(
                    "Unknown.Release.2026-GRP",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateApiResponse<XrelP2PRelease>(HttpStatusCode.NotFound));

        // Act
        var result = await service.GetReleaseNfoAsync(
            new XrelConfig(),
            "Unknown.Release.2026-GRP",
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();

        httpClientFactoryMock.Verify(
            factory => factory.CreateClient(XrelClient.NfoPageHttpClientName),
            Times.Never
        );
    }

    private void SetupP2pRelease(string dirname)
    {
        apiMock
            .Setup(api => api.GetP2pReleaseInfoAsync(dirname, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new XrelP2PRelease(
                        Dirname: dirname,
                        LinkHref: "https://www.xrel.to/p2p/42-P2P-Release/nfo.html",
                        SizeMb: 11900,
                        ExtInfo: null
                    )
                )
            );
    }

    private void SetupNfoPage(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(XrelClient.NfoPageHttpClientName))
            .Returns(() => CreateHttpClient(responseFactory));
    }

    private static HttpResponseMessage CreateHtmlResponse(HttpStatusCode statusCode, string html)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        };
    }

    private static ApiResponse<T> CreateApiResponse<T>(
        HttpStatusCode statusCode,
        T? content = default,
        IReadOnlyDictionary<string, string>? headers = null
    )
    {
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = new HttpRequestMessage(),
        };
        foreach (var header in headers ?? new Dictionary<string, string>())
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return new ApiResponse<T>(response, content!, new RefitSettings(), error: null);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory
    )
    {
        return new HttpClient(new DelegateHttpMessageHandler(responseFactory));
    }

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
