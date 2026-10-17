using System.Net;
using System.Text;
using System.Web;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Vixeo;
using Bearcat.Hosters.Vixeo.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Vixeo;

public class VixeoTest
{
    private const string ApiKey = "vixeo-api-key";

    private const string UploadUrl = "https://fi-enc-0.upload.test/api/v1/upload?key=vixeo-api-key";

    private const string InvalidApiKeyResponse =
        """{"success":false,"message":"Invalid API key"}""";

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private Hosters.Vixeo.Vixeo hoster = null!;
    private VixeoConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();

        var api = RestService.For<IVixeoApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.ApiBaseUrl),
            }
        );

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        hoster = new Hosters.Vixeo.Vixeo(
            new ApiClient(api, new HttpClientProvider(httpClientFactoryMock.Object)),
            Mock.Of<ILogger<Hosters.Vixeo.Vixeo>>()
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new VixeoConfig { ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mkv");
        await File.WriteAllTextAsync(filePath, "vixeo test video");
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_ReturnsEmbedUrlAndVideoId()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            request.RequestUri!.AbsolutePath == "/api/v1/getUploadSrv"
                ? (
                    HttpStatusCode.OK,
                    $$"""{"success":true,"server":"{{UploadUrl}}","result":"{{UploadUrl}}"}"""
                )
                : (
                    HttpStatusCode.OK,
                    """{"success":true,"message":"Video uploaded successfully","data":{"id":"okzqdNz9QXv1","filename":"tkDF3DchibZb0vH.mkv","title":"bearcat-test.mkv","size":70773,"mimeType":"video","status":"pending","url":"https://vixeo.io/e/okzqdNz9QXv1"}}"""
                )
        );

        // Act
        var result = await hoster.UploadFileAsync(
            new FileDto(Id: 7, FullFileName: filePath, UploadId: 1),
            config,
            Mock.Of<ITransferProgress>(),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://vixeo.io/e/okzqdNz9QXv1");
        result.EmbedUrl.ShouldBe("https://vixeo.io/e/okzqdNz9QXv1");
        result.ExternalId.ShouldBe("okzqdNz9QXv1");
        HttpUtility
            .ParseQueryString(httpMessageHandler.Requests.First().Uri.Query)["key"]
            .ShouldBe(ApiKey);
        var uploadRequest = httpMessageHandler.Requests.Last();
        uploadRequest.Method.ShouldBe(HttpMethod.Post);
        uploadRequest.Uri.ShouldBe(new Uri(UploadUrl));
        uploadRequest.Body.ShouldContain($"name=apiKey\r\n\r\n{ApiKey}");
        uploadRequest.Body.ShouldContain("name=video; filename=");
        uploadRequest.Body.ShouldContain("vixeo test video");
    }

    [Test]
    public async Task UploadFileAsync_ApiKeyRejected_ThrowsHosterCredentialsRejectedException()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => (HttpStatusCode.Unauthorized, InvalidApiKeyResponse));

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(async () =>
            await hoster.UploadFileAsync(
                new FileDto(Id: 7, FullFileName: filePath, UploadId: 1),
                config,
                Mock.Of<ITransferProgress>(),
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("Invalid API key");
        httpMessageHandler.Requests.Count.ShouldBe(1);
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyRejected_ReturnsFailureWithMessage()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => (HttpStatusCode.Unauthorized, InvalidApiKeyResponse));

        // Act
        var result = await hoster.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("Invalid API key");
        httpMessageHandler.Requests.Single().Uri.AbsolutePath.ShouldBe("/api/v1/account");
    }

    [Test]
    public async Task CheckFilesExistAsync_StatusResponses_MapsOnlyOnlineToOnlineAndNotFoundToOffline()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            HttpUtility.ParseQueryString(request.RequestUri!.Query)["code"] switch
            {
                "onlineid" => (HttpStatusCode.OK, """{"success":true,"status":"online"}"""),
                "downloadid" => (HttpStatusCode.OK, """{"success":true,"status":"online"}"""),
                "offlineid" => (HttpStatusCode.OK, """{"success":true,"status":"offline"}"""),
                "deletedid" => (HttpStatusCode.OK, """{"success":true,"status":"deleted"}"""),
                _ => (HttpStatusCode.NotFound, """{"success":false,"message":"Video not found"}"""),
            }
        );

        // Act
        var result = await hoster.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto("https://vixeo.io/e/onlineid", "onlineid"),
                new FileUrlToCheckDto("https://vidsonic.net/d/downloadid", ExternalId: null),
                new FileUrlToCheckDto("https://vixeo.io/e/offlineid", ExternalId: null),
                new FileUrlToCheckDto("https://vixeo.io/e/deletedid", "deletedid"),
                new FileUrlToCheckDto("https://vixeo.io/e/unknownid", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl.ShouldBe(
            new Dictionary<string, bool>
            {
                ["https://vixeo.io/e/onlineid"] = true,
                ["https://vidsonic.net/d/downloadid"] = true,
                ["https://vixeo.io/e/offlineid"] = false,
                ["https://vixeo.io/e/deletedid"] = false,
                ["https://vixeo.io/e/unknownid"] = false,
            },
            ignoreOrder: true
        );
        httpMessageHandler
            .Requests.Select(request => request.Uri.AbsolutePath)
            .ShouldAllBe(path => path == "/api/v1/check-status");
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body);

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, (HttpStatusCode StatusCode, string Content)> respond = _ =>
            throw new InvalidOperationException("No response configured");

        public List<RecordedRequest> Requests { get; } = [];

        public void RespondWith(
            Func<HttpRequestMessage, (HttpStatusCode StatusCode, string Content)> responseFactory
        )
        {
            respond = responseFactory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));

            var (statusCode, content) = respond(request);

            return new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
        }
    }
}
