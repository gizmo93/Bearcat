using System.Globalization;
using System.Net;
using Bearcat.Abstractions.ImageHoster.Exceptions;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.Shared;
using Shouldly;

namespace Bearcat.ImageHosters.UnitTest.Shared;

public class ImageUrlExistenceCheckerTest
{
    private const string FullUrl = "https://example.test/images/full.jpg";
    private const string ThumbnailUrl = "https://example.test/images/thumbnail.jpg";
    private const string RemovedImagePlaceholderUrl = "https://example.test/image/removed.png";

    private static readonly List<ImageUrl> ImageUrls =
    [
        new(ImageSize.Full, FullUrl),
        new(ImageSize.Thumbnail, ThumbnailUrl),
    ];

    private StatusCodeHandler handler = null!;
    private HttpClient httpClient = null!;
    private ImageUrlExistenceChecker checker = null!;

    [SetUp]
    public void SetUp()
    {
        handler = new StatusCodeHandler();
        httpClient = new HttpClient(handler);
        checker = new ImageUrlExistenceChecker(httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        httpClient.Dispose();
        handler.Dispose();
    }

    [Test]
    public async Task CheckAllImageUrlsExistAsync_AllUrlsReturnOk_ReturnsTrue()
    {
        // Act
        var exists = await checker.CheckAllImageUrlsExistAsync(ImageUrls, CancellationToken.None);

        // Assert
        exists.ShouldBeTrue();
        handler.RequestedUrls.ShouldBe([FullUrl, ThumbnailUrl]);
        handler.RequestMethods.ShouldAllBe(method => method == HttpMethod.Head);
    }

    [Test]
    public async Task CheckAllImageUrlsExistAsync_OneUrlReturnsNotFound_ReturnsFalse()
    {
        // Arrange
        handler.StatusCodesByUrl[ThumbnailUrl] = HttpStatusCode.NotFound;

        // Act
        var exists = await checker.CheckAllImageUrlsExistAsync(ImageUrls, CancellationToken.None);

        // Assert
        exists.ShouldBeFalse();
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task CheckAllImageUrlsExistAsync_UnexpectedStatusCode_Throws(
        HttpStatusCode statusCode
    )
    {
        // Arrange
        handler.StatusCodesByUrl[FullUrl] = statusCode;

        // Act
        var exception = await Should.ThrowAsync<ImageOnlineCheckFailedException>(() =>
            checker.CheckAllImageUrlsExistAsync(ImageUrls, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain(((int)statusCode).ToString(CultureInfo.InvariantCulture));
        exception.Message.ShouldContain(FullUrl);
    }

    [Test]
    public async Task CheckAllImageUrlsExistAsync_FinalUrlIsRemovedImagePlaceholder_ReturnsFalse()
    {
        // Arrange
        handler.RedirectTargetsByUrl[ThumbnailUrl] = RemovedImagePlaceholderUrl;

        // Act
        var exists = await checker.CheckAllImageUrlsExistAsync(
            imageUrls: ImageUrls,
            removedImagePlaceholderUrl: RemovedImagePlaceholderUrl,
            cancellationToken: CancellationToken.None
        );

        // Assert
        exists.ShouldBeFalse();
    }

    private sealed class StatusCodeHandler : HttpMessageHandler
    {
        public Dictionary<string, HttpStatusCode> StatusCodesByUrl { get; } = [];
        public Dictionary<string, string> RedirectTargetsByUrl { get; } = [];
        public List<string> RequestedUrls { get; } = [];
        public List<HttpMethod> RequestMethods { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var requestedUrl = request.RequestUri!.AbsoluteUri;
            RequestedUrls.Add(requestedUrl);
            RequestMethods.Add(request.Method);

            var statusCode = StatusCodesByUrl.GetValueOrDefault(requestedUrl, HttpStatusCode.OK);
            var response = new HttpResponseMessage(statusCode) { RequestMessage = request };

            if (RedirectTargetsByUrl.TryGetValue(requestedUrl, out var redirectTarget))
            {
                response.RequestMessage = new HttpRequestMessage(request.Method, redirectTarget);
            }

            return Task.FromResult(response);
        }
    }
}
