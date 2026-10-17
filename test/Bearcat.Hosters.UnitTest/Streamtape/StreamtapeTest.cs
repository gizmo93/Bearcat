using System.Net;
using System.Text;
using System.Web;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Streamtape;
using Bearcat.Hosters.Streamtape.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Streamtape;

public class StreamtapeTest
{
    private const string ApiLogin = "63e2b321d270e84a7431";

    private const string ApiKey = "streamtape-api-key";

    private const string UploadUrl =
        "https://908373049.tapecontent.net/xfsproupload/L9i53lKpulH2h0Gq";

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private Hosters.Streamtape.Streamtape hoster = null!;
    private StreamtapeConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();

        var api = RestService.For<IStreamtapeApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.ApiBaseUrl),
            }
        );

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        hoster = new Hosters.Streamtape.Streamtape(
            new ApiClient(
                api,
                new HttpClientProvider(httpClientFactoryMock.Object),
                Mock.Of<ILogger<ApiClient>>()
            ),
            Mock.Of<ILogger<Hosters.Streamtape.Streamtape>>()
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new StreamtapeConfig { ApiLogin = ApiLogin, ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        await File.WriteAllTextAsync(filePath, "streamtape test video");
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_FolderIdSet_RequestsUploadUrlForFolderAndReturnsFileUrlIdAndEmbedUrl()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            request.RequestUri!.AbsolutePath == "/file/ul"
                ? $$$"""{"status":200,"msg":"OK","result":{"url":"{{{UploadUrl}}}","valid_until":"2026-10-17 22:26:02"}}"""
                : """{"status":200,"msg":"Upload finished!","result":{"url":"https://streamtape.com/v/dOrol7BK0oikmlg/bearcat_test_clip.mp4","sha256":"abc","name":"bearcat_test_clip.mp4","size":"72195","content_type":"video/mp4","id":"dOrol7BK0oikmlg"}}"""
        );

        // Act
        var result = await hoster.UploadFileAsync(
            new FileDto(Id: 7, FullFileName: filePath, UploadId: 1, FolderId: "bG9dFOcK9UA"),
            config,
            Mock.Of<ITransferProgress>(),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://streamtape.com/v/dOrol7BK0oikmlg/bearcat_test_clip.mp4");
        result.ExternalId.ShouldBe("dOrol7BK0oikmlg");
        result.EmbedUrl.ShouldBe("https://streamtape.com/e/dOrol7BK0oikmlg");
        httpMessageHandler.Requests.Count.ShouldBe(2);
        var uploadUrlQuery = HttpUtility.ParseQueryString(httpMessageHandler.Requests[0].Uri.Query);
        uploadUrlQuery["login"].ShouldBe(ApiLogin);
        uploadUrlQuery["key"].ShouldBe(ApiKey);
        uploadUrlQuery["folder"].ShouldBe("bG9dFOcK9UA");
        var uploadRequest = httpMessageHandler.Requests[1];
        uploadRequest.Method.ShouldBe(HttpMethod.Post);
        uploadRequest.Uri.ShouldBe(new Uri(UploadUrl));
        uploadRequest.Body.ShouldContain("name=\"file1\"");
        uploadRequest.Body.ShouldNotContain("filename*");
        uploadRequest.Body.ShouldContain("streamtape test video");
    }

    [Test]
    public async Task UploadFileAsync_AuthenticationFailed_ThrowsHosterCredentialsRejectedExceptionWithoutRetry()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            """{"status":403,"msg":"Authentication failed","result":null}"""
        );

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
        exception.Message.ShouldContain("Authentication failed");
        httpMessageHandler.Requests.Count.ShouldBe(1);
    }

    [Test]
    public async Task CheckFilesExistAsync_FileInfoEntries_MapsOnlyStatus200ToOnline()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            """
                {"status":200,"msg":"OK","result":{
                  "onlineid":{"id":"onlineid","status":200,"name":"clip.mp4","size":71186,"converted":true},
                  "unknownid":{"id":"unknownid","status":404,"name":false,"size":0,"converted":false},
                  "deletedid":{"id":"deletedid","status":500,"name":"clip.mp4","size":71186,"converted":true}
                }}
                """
        );

        // Act
        var result = await hoster.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto("https://streamtape.com/v/onlineid/clip.mp4", "onlineid"),
                new FileUrlToCheckDto("https://streamtape.com/e/unknownid", ExternalId: null),
                new FileUrlToCheckDto("https://streamtape.com/v/deletedid/clip.mp4", null),
                new FileUrlToCheckDto("https://streamtape.com/e/missingid", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl.ShouldBe(
            new Dictionary<string, bool>
            {
                ["https://streamtape.com/v/onlineid/clip.mp4"] = true,
                ["https://streamtape.com/e/unknownid"] = false,
                ["https://streamtape.com/v/deletedid/clip.mp4"] = false,
                ["https://streamtape.com/e/missingid"] = false,
            },
            ignoreOrder: true
        );
        var query = HttpUtility.ParseQueryString(httpMessageHandler.Requests.Single().Uri.Query);
        query["file"]!.Split(',').ShouldBe(["onlineid", "unknownid", "deletedid", "missingid"]);
    }

    [Test]
    public async Task CreateFolderAsync_RootFolderWithSameName_ReturnsExistingFolderIdWithoutCreatingFolder()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            """{"status":200,"msg":"OK","result":{"folders":[{"id":"c4ZJo99FS68","name":"Subtitles"},{"id":"bG9dFOcK9UA","name":"bearcat.release"}],"files":[]}}"""
        );

        // Act
        var result = await hoster.CreateFolderAsync(
            "Bearcat.Release",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("bG9dFOcK9UA");
        httpMessageHandler
            .Requests.Select(request => request.Uri.AbsolutePath)
            .ShouldBe(["/file/listfolder"]);
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body);

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, string> respond = _ =>
            throw new InvalidOperationException("No response configured");

        public List<RecordedRequest> Requests { get; } = [];

        public void RespondWith(Func<HttpRequestMessage, string> responseFactory)
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

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(respond(request), Encoding.UTF8, "application/json"),
            };
        }
    }
}
