using System.Net;
using System.Text;
using System.Web;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Videro;
using Bearcat.Hosters.Videro.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Videro;

public class VideroTest
{
    private const string ApiKey = "videro-api-key";

    private const string UploadUrl = $"https://videro.im/api/v1/upload?key={ApiKey}";

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private Hosters.Videro.Videro hoster = null!;
    private VideroConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();

        var api = RestService.For<IVideroApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.ApiBaseUrl),
            }
        );

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        hoster = new Hosters.Videro.Videro(
            new ApiClient(
                api,
                new HttpClientProvider(httpClientFactoryMock.Object),
                Mock.Of<ILogger<ApiClient>>()
            ),
            Mock.Of<ILogger<Hosters.Videro.Videro>>()
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new VideroConfig { ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mkv");
        await File.WriteAllTextAsync(filePath, "videro test video");
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_ReturnsVideroMyUrls()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/upload/server", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, $$"""{"status":200,"result":"{{UploadUrl}}"}""")
                : (
                    HttpStatusCode.OK,
                    """{"msg":"OK","status":200,"result":[{"download_url":"https://videro.im/d/4McPXUpL8bry","filecode":"4McPXUpL8bry","size":"72195","status":200}]}"""
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
        result.FileUrl.ShouldBe("https://videro.my/d/4McPXUpL8bry");
        result.EmbedUrl.ShouldBe("https://videro.my/e/4McPXUpL8bry");
        result.ExternalId.ShouldBe("4McPXUpL8bry");
        var uploadRequest = httpMessageHandler.Requests.Last();
        uploadRequest.Method.ShouldBe(HttpMethod.Post);
        uploadRequest.Uri.ShouldBe(new Uri(UploadUrl));
        uploadRequest.Body.ShouldContain($"name=key\r\n\r\n{ApiKey}");
        uploadRequest.Body.ShouldContain("name=file; filename=");
    }

    [Test]
    public async Task CheckFilesExistAsync_FileNotFound_RequestsEachFileCodeSeparatelyAndMapsNotFoundToOffline()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            HttpUtility.ParseQueryString(request.RequestUri!.Query)["file_code"] == "readycode"
                ? (
                    HttpStatusCode.OK,
                    """{"status":200,"result":[{"file_code":"readycode","status":"processing"}]}"""
                )
                : (
                    HttpStatusCode.NotFound,
                    """{"status":404,"msg":"File not found or access denied"}"""
                )
        );

        // Act
        var result = await hoster.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto("https://videro.my/d/readycode", "readycode"),
                new FileUrlToCheckDto("https://videro.my/e/unknowncode", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl.ShouldBe(
            new Dictionary<string, bool>
            {
                ["https://videro.my/d/readycode"] = true,
                ["https://videro.my/e/unknowncode"] = false,
            },
            ignoreOrder: true
        );
        httpMessageHandler
            .Requests.Select(request =>
                HttpUtility.ParseQueryString(request.Uri.Query)["file_code"]
            )
            .ShouldBe(["readycode", "unknowncode"], ignoreOrder: true);
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyRejectedWithForbidden_ReturnsFailure()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            (HttpStatusCode.Forbidden, """{"status":403,"msg":"Invalid API Key"}""")
        );

        // Act
        var result = await hoster.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Videro rejected the API key: Invalid API Key");
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body);

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, (HttpStatusCode StatusCode, string Body)> respond = _ =>
            throw new InvalidOperationException("No response configured");

        public List<RecordedRequest> Requests { get; } = [];

        public void RespondWith(
            Func<HttpRequestMessage, (HttpStatusCode StatusCode, string Body)> responseFactory
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

            var (statusCode, responseBody) = respond(request);

            return new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
