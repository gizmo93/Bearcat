using System.Net;
using System.Text;
using System.Web;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Vidara;
using Bearcat.Hosters.Vidara.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Vidara;

public class VidaraTest
{
    private const string ApiKey = "vidara-api-key";

    private const string UploadServerUrl = "https://upl4.s1q2105.com/api/upload";

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private Hosters.Vidara.Vidara hoster = null!;
    private VidaraConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();

        var api = RestService.For<IVidaraApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.ApiBaseUrl),
            }
        );

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        hoster = new Hosters.Vidara.Vidara(
            new ApiClient(
                api,
                new HttpClientProvider(httpClientFactoryMock.Object),
                Mock.Of<ILogger<ApiClient>>()
            ),
            Mock.Of<ILogger<Hosters.Vidara.Vidara>>()
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new VidaraConfig { ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mkv");
        await File.WriteAllTextAsync(filePath, "vidara test video");
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_PostsApiKeyFieldAndReturnsVidaraUrls()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/upload/server", StringComparison.Ordinal)
                ? (
                    HttpStatusCode.OK,
                    $$$"""{"msg":"OK","status":200,"result":{"upload_server":"{{{UploadServerUrl}}}"}}"""
                )
                : (
                    HttpStatusCode.OK,
                    """{"filecode":"https://vidara.to/e/3zwDpy6bDYp1d","video_id":4734485,"title":"bearcat_test_clip"}"""
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
        result.FileUrl.ShouldBe("https://vidara.to/3zwDpy6bDYp1d");
        result.EmbedUrl.ShouldBe("https://vidara.to/e/3zwDpy6bDYp1d");
        result.ExternalId.ShouldBe("3zwDpy6bDYp1d");
        var uploadRequest = httpMessageHandler.Requests.Last();
        uploadRequest.Method.ShouldBe(HttpMethod.Post);
        uploadRequest.Uri.ShouldBe(new Uri(UploadServerUrl));
        uploadRequest.Body.ShouldContain($"name=api_key\r\n\r\n{ApiKey}");
        uploadRequest.Body.ShouldContain("name=file; filename=");
    }

    [Test]
    public async Task CheckFilesExistAsync_MixedStatuses_RequestsEachFileCodeSeparatelyAndMapsDeletedAndUnknownToOffline()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            HttpUtility.ParseQueryString(request.RequestUri!.Query)["filecode"] switch
            {
                "activecode" => (
                    HttpStatusCode.OK,
                    """{"status":200,"result":[{"status":"active","filecode":"activecode","link":"https://vidara.to/activecode","file_active":1},{"status":"active","filecode":"activecode"}]}"""
                ),
                "deletedcode" => (
                    HttpStatusCode.OK,
                    """{"status":200,"result":[{"status":"deleted","filecode":"deletedcode","link":"https://vidara.to/deletedcode","file_active":0}]}"""
                ),
                _ => (
                    HttpStatusCode.Forbidden,
                    """{"server_time":"2026-10-17 12:00:00","status":403}"""
                ),
            }
        );

        // Act
        var result = await hoster.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto("https://vidara.to/activecode", "activecode"),
                new FileUrlToCheckDto("https://vidara.to/deletedcode", "deletedcode"),
                new FileUrlToCheckDto("https://vidara.to/e/unknowncode", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl.ShouldBe(
            new Dictionary<string, bool>
            {
                ["https://vidara.to/activecode"] = true,
                ["https://vidara.to/deletedcode"] = false,
                ["https://vidara.to/e/unknowncode"] = false,
            },
            ignoreOrder: true
        );
        httpMessageHandler
            .Requests.Select(request => HttpUtility.ParseQueryString(request.Uri.Query))
            .Select(query => (query["api_key"], query["filecode"]))
            .ShouldBe(
                [(ApiKey, "activecode"), (ApiKey, "deletedcode"), (ApiKey, "unknowncode")],
                ignoreOrder: true
            );
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyRejectedWithPlainTextUnauthorized_ReturnsFailure()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => (HttpStatusCode.Unauthorized, "invalid api_key"));

        // Act
        var result = await hoster.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Vidara rejected the API key: invalid api_key");
    }

    [Test]
    public async Task CreateFolderAsync_FolderDoesNotExist_CreatesFolderAndReturnsFolderId()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/folder/list", StringComparison.Ordinal)
                ? (
                    HttpStatusCode.OK,
                    """{"msg":"OK","result":{"folders":[{"code":11111,"fld_id":11111,"name":"other-folder","videos":0}],"results":1},"status":200}"""
                )
                : (
                    HttpStatusCode.OK,
                    """{"msg":"OK","result":{"folder_id":36469,"name":"bearcat-release"},"status":200}"""
                )
        );

        // Act
        var folderId = await hoster.CreateFolderAsync(
            "bearcat-release",
            config,
            CancellationToken.None
        );

        // Assert
        folderId.ShouldBe("36469");
        var creationRequest = httpMessageHandler.Requests.Last();
        creationRequest.Uri.AbsolutePath.ShouldEndWith("/folder/create");
        HttpUtility.ParseQueryString(creationRequest.Uri.Query)["name"].ShouldBe("bearcat-release");
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
