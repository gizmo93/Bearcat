using System.Net;
using System.Text;
using System.Web;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.DoodStream;
using Bearcat.Hosters.DoodStream.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.DoodStream;

public class DoodStreamTest
{
    private const string ApiKey = "doodstream-api-key";

    private const string UploadServerUrl = "https://tr1128ve.upload.test/upload/01";

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private Hosters.DoodStream.DoodStream hoster = null!;
    private DoodStreamConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();

        var api = RestService.For<IDoodStreamApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.ApiBaseUrl),
            }
        );

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        hoster = new Hosters.DoodStream.DoodStream(
            new ApiClient(
                api,
                new HttpClientProvider(httpClientFactoryMock.Object),
                Mock.Of<ILogger<ApiClient>>()
            ),
            Mock.Of<ILogger<Hosters.DoodStream.DoodStream>>()
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new DoodStreamConfig { ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mkv");
        await File.WriteAllTextAsync(filePath, "doodstream test video");
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_PostsApiKeyAsQueryAndReturnsDoodStreamUrls()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/upload/server", StringComparison.Ordinal)
                ? $$"""{"result":"{{UploadServerUrl}}","status":200,"msg":"OK"}"""
                : """{"result":[{"download_url":"https://dsvplay.com/d/4rldg3km7gco","status":200,"protected_embed":"https://dsvplay.com/e/4rldg3km7gco","filecode":"4rldg3km7gco"}],"msg":"OK","status":200}"""
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
        result.FileUrl.ShouldBe("https://doodstream.com/d/4rldg3km7gco");
        result.EmbedUrl.ShouldBe("https://doodstream.com/e/4rldg3km7gco");
        result.ExternalId.ShouldBe("4rldg3km7gco");
        var uploadRequest = httpMessageHandler.Requests.Last();
        uploadRequest.Method.ShouldBe(HttpMethod.Post);
        uploadRequest.Uri.ShouldBe(new Uri($"{UploadServerUrl}?{ApiKey}"));
        uploadRequest.Body.ShouldContain($"name=api_key\r\n\r\n{ApiKey}");
        uploadRequest.Body.ShouldContain("name=file; filename=");
        uploadRequest.Body.ShouldContain("doodstream test video");
    }

    [Test]
    public async Task UploadFileAsync_InvalidKey_ThrowsHosterCredentialsRejectedException()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => """{"status":400,"msg":"Invalid key"}""");

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
        exception.Message.ShouldContain("Invalid key");
        httpMessageHandler.Requests.Count.ShouldBe(1);
    }

    [Test]
    public async Task CheckFilesExistAsync_FileCodeOmittedFromFileInfo_MapsToOffline()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            """{"msg":"OK","status":200,"result":[{"filecode":"onlinecode","status":200,"canplay":1}]}"""
        );

        // Act
        var result = await hoster.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto("https://doodstream.com/d/onlinecode", "onlinecode"),
                new FileUrlToCheckDto("https://doodstream.com/d/missingcode", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl.ShouldBe(
            new Dictionary<string, bool>
            {
                ["https://doodstream.com/d/onlinecode"] = true,
                ["https://doodstream.com/d/missingcode"] = false,
            },
            ignoreOrder: true
        );
        var query = HttpUtility.ParseQueryString(httpMessageHandler.Requests.Single().Uri.Query);
        query["key"].ShouldBe(ApiKey);
        query["file_code"]!.Split(',').ShouldBe(["onlinecode", "missingcode"], ignoreOrder: true);
    }

    [Test]
    public async Task CheckFilesExistAsync_AllFileCodesInvalid_MapsAllToOffline()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => """{"msg":"Invalid file codes","status":400}""");

        // Act
        var result = await hoster.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto("https://doodstream.com/d/deletedcode", "deletedcode"),
                new FileUrlToCheckDto("https://doodstream.com/e/unknowncode", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl.ShouldBe(
            new Dictionary<string, bool>
            {
                ["https://doodstream.com/d/deletedcode"] = false,
                ["https://doodstream.com/e/unknowncode"] = false,
            },
            ignoreOrder: true
        );
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
