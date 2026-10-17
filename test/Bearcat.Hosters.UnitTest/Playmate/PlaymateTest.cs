using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Playmate;
using Bearcat.Hosters.Playmate.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Playmate;

public class PlaymateTest
{
    private const string ApiKey = "playmate-api-key";

    private const string UploadUrl = "https://srv1.upload.test/up7k2/api";

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private Hosters.Playmate.Playmate hoster = null!;
    private PlaymateConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();

        var api = RestService.For<IPlaymateApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.ApiBaseUrl),
            }
        );

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        hoster = new Hosters.Playmate.Playmate(
            new ApiClient(
                api,
                new HttpClientProvider(httpClientFactoryMock.Object),
                Mock.Of<ILogger<ApiClient>>()
            ),
            Mock.Of<ILogger<Hosters.Playmate.Playmate>>()
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new PlaymateConfig { ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mkv");
        await File.WriteAllTextAsync(filePath, "playmate test video");
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_UsesReturnedFieldNamesAndReturnsEmbedUrlAndFileCode()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            request.RequestUri!.AbsolutePath == "/upload/server"
                ? $$"""{"msg":"OK","result":{"file_field":"vsrc","key_field":"api_key","method":"POST","upload_url":"{{UploadUrl}}"},"status":200}"""
                : """{"filecode":"https://playmate.to/embed/X2or9didXf81x","video_id":469733,"title":"bearcat-test"}"""
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
        result.FileUrl.ShouldBe("https://playmate.to/embed/X2or9didXf81x");
        result.EmbedUrl.ShouldBe("https://playmate.to/embed/X2or9didXf81x");
        result.ExternalId.ShouldBe("X2or9didXf81x");
        var uploadRequest = httpMessageHandler.Requests.Last();
        uploadRequest.Method.ShouldBe(HttpMethod.Post);
        uploadRequest.Uri.ShouldBe(new Uri(UploadUrl));
        uploadRequest.Body.ShouldContain($"name=api_key\r\n\r\n{ApiKey}");
        uploadRequest.Body.ShouldContain("name=vsrc; filename=");
        uploadRequest.Body.ShouldContain("playmate test video");
    }

    [Test]
    public async Task UploadFileAsync_InvalidApiKey_ThrowsHosterCredentialsRejectedException()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => """{"msg":"Invalid API key","status":401}""");

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
    public async Task CheckFilesExistAsync_FileInfoEntries_MapsNotFoundAndDeletedToOfflineByFileCode()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            """
                {"msg":"OK","result":[
                  {"file_code":"activecode","canplay":1,"length":"5","state":"active","status":200},
                  {"file_code":"embedcode","state":"active","status":200},
                  {"file_code":"deletedcode","state":"deleted","status":200},
                  {"file_code":"unknowncode","status":404}
                ],"status":200}
                """
        );

        // Act
        var result = await hoster.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto("https://playmate.to/embed/activecode", "activecode"),
                new FileUrlToCheckDto("https://playmate.to/embed/embedcode", ExternalId: null),
                new FileUrlToCheckDto("https://playmate.to/embed/deletedcode", "deletedcode"),
                new FileUrlToCheckDto("https://playmate.to/embed/unknowncode", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl.ShouldBe(
            new Dictionary<string, bool>
            {
                ["https://playmate.to/embed/activecode"] = true,
                ["https://playmate.to/embed/embedcode"] = true,
                ["https://playmate.to/embed/deletedcode"] = false,
                ["https://playmate.to/embed/unknowncode"] = false,
            },
            ignoreOrder: true
        );
        var query = HttpUtility.ParseQueryString(httpMessageHandler.Requests.Single().Uri.Query);
        query["key"].ShouldBe(ApiKey);
        query["file_code"]!
            .Split(',')
            .ShouldBe(["activecode", "embedcode", "deletedcode", "unknowncode"], ignoreOrder: true);
    }

    [Test]
    public async Task CheckFilesExistAsync_MoreThanFiftyFiles_RequestsFileInfoInBatchesOfFifty()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
        {
            var fileCodes = HttpUtility.ParseQueryString(request.RequestUri!.Query)[
                "file_code"
            ]!.Split(',');
            var result = fileCodes
                .Select(fileCode => new
                {
                    file_code = fileCode,
                    state = "active",
                    status = 200,
                })
                .ToList();

            return JsonSerializer.Serialize(
                new
                {
                    msg = "OK",
                    result,
                    status = 200,
                }
            );
        });
        var files = Enumerable
            .Range(1, 51)
            .Select(index => new FileUrlToCheckDto($"https://playmate.to/embed/code{index}", null))
            .ToList();

        // Act
        var result = await hoster.CheckFilesExistAsync(config, files, CancellationToken.None);

        // Assert
        result.StatusPerFileUrl.Count.ShouldBe(51);
        result.StatusPerFileUrl.Values.ShouldAllBe(isOnline => isOnline);
        httpMessageHandler
            .Requests.Select(request =>
                HttpUtility.ParseQueryString(request.Uri.Query)["file_code"]!.Split(',').Length
            )
            .ShouldBe([50, 1]);
    }

    [Test]
    public async Task CreateFolderAsync_FolderWithSameNameExists_ReturnsExistingFolderIdWithoutCreatingFolder()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            """{"msg":"OK","result":[{"fld_id":2072,"name":"Other.Release","parent_id":0},{"fld_id":2073,"name":"bearcat.release","parent_id":0}],"status":200}"""
        );

        // Act
        var result = await hoster.CreateFolderAsync(
            "Bearcat.Release",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("2073");
        httpMessageHandler
            .Requests.Select(request => request.Uri.AbsolutePath)
            .ShouldBe(["/folder/list"]);
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
