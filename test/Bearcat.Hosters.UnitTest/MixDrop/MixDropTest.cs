using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.MixDrop;
using Bearcat.Hosters.MixDrop.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.MixDrop;

public class MixDropTest
{
    private const string EmailAddress = "uploader@example.com";

    private const string ApiKey = "mixdrop-api-key";

    private const string InvalidLoginResponse =
        """{"success":false,"result":{"msg":"Invalid login"}}""";

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private Hosters.MixDrop.MixDrop hoster = null!;
    private MixDropConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();

        var api = RestService.For<IMixDropApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.ApiBaseUrl),
            }
        );

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        hoster = new Hosters.MixDrop.MixDrop(
            new ApiClient(
                api,
                new HttpClientProvider(httpClientFactoryMock.Object),
                Mock.Of<ILogger<ApiClient>>()
            ),
            Mock.Of<ILogger<Hosters.MixDrop.MixDrop>>()
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new MixDropConfig { EmailAddress = EmailAddress, ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mkv");
        await File.WriteAllTextAsync(filePath, "mixdrop test video");
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_ReturnsFileUrlFileReferenceAndEmbedUrl()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            """
                {"success":true,"result":{"fileref":"k00lvwe1t0609k","url":"https://mixdrop.top/f/k00lvwe1t0609k","embedurl":"https://mixdrop.top/e/k00lvwe1t0609k"}}
                """
        );

        // Act
        var result = await hoster.UploadFileAsync(
            new FileDto(Id: 7, FullFileName: filePath, UploadId: 1, FolderId: "1104212"),
            config,
            Mock.Of<ITransferProgress>(),
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://mixdrop.top/f/k00lvwe1t0609k");
        result.ExternalId.ShouldBe("k00lvwe1t0609k");
        result.EmbedUrl.ShouldBe("https://mixdrop.top/e/k00lvwe1t0609k");
        var request = httpMessageHandler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ShouldBe(new Uri(ApiClient.UploadUrl));
        request.Body.ShouldContain($"name=email\r\n\r\n{EmailAddress}");
        request.Body.ShouldContain($"name=key\r\n\r\n{ApiKey}");
        request.Body.ShouldContain("name=folder\r\n\r\n1104212");
        request.Body.ShouldContain("mixdrop test video");
    }

    [Test]
    public async Task UploadFileAsync_InvalidLogin_ThrowsHosterCredentialsRejectedException()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => InvalidLoginResponse);

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
        exception.Message.ShouldContain("Invalid login");
        httpMessageHandler.Requests.Count.ShouldBe(1);
    }

    [Test]
    public async Task CheckFilesExistAsync_FileInfoEntries_MapsDeletedAndNotFoundToOfflineByFileReference()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ =>
            """
                {"success":true,"result":{
                  "onlineref":{"fileref":"onlineref","status":"Converting ( 40% )","deleted":false,"url":"https://mxdrop.sx/f/onlineref"},
                  "rotatedref":{"fileref":"rotatedref","status":"OK","deleted":false,"url":"https://mxdrop.sx/f/rotatedref"},
                  "deletedref":{"fileref":"deletedref","status":"OK","deleted":true},
                  "unknownref":{"fileref":"unknownref","status":"notfound"}
                }}
                """
        );

        // Act
        var result = await hoster.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto("https://mixdrop.top/f/onlineref", "onlineref"),
                new FileUrlToCheckDto("https://mixdrop.top/e/rotatedref", ExternalId: null),
                new FileUrlToCheckDto("https://mixdrop.top/f/deletedref", "deletedref"),
                new FileUrlToCheckDto("https://mixdrop.top/f/unknownref", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl.ShouldBe(
            new Dictionary<string, bool>
            {
                ["https://mixdrop.top/f/onlineref"] = true,
                ["https://mixdrop.top/e/rotatedref"] = true,
                ["https://mixdrop.top/f/deletedref"] = false,
                ["https://mixdrop.top/f/unknownref"] = false,
            },
            ignoreOrder: true
        );
        var query = HttpUtility.ParseQueryString(httpMessageHandler.Requests.Single().Uri.Query);
        query["email"].ShouldBe(EmailAddress);
        query["key"].ShouldBe(ApiKey);
        query
            .GetValues("ref[]")
            .ShouldBe(["onlineref", "rotatedref", "deletedref", "unknownref"], ignoreOrder: true);
    }

    [Test]
    public async Task CheckFilesExistAsync_MoreThanFiftyFiles_RequestsFileInfoInBatchesOfFifty()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
        {
            var fileReferences = HttpUtility
                .ParseQueryString(request.RequestUri!.Query)
                .GetValues("ref[]")!;
            var result = fileReferences.ToDictionary(
                fileReference => fileReference,
                fileReference => new
                {
                    fileref = fileReference,
                    status = "OK",
                    deleted = false,
                }
            );

            return JsonSerializer.Serialize(new { success = true, result });
        });
        var files = Enumerable
            .Range(1, 51)
            .Select(index => new FileUrlToCheckDto($"https://mixdrop.top/f/ref{index}", null))
            .ToList();

        // Act
        var result = await hoster.CheckFilesExistAsync(config, files, CancellationToken.None);

        // Assert
        result.StatusPerFileUrl.Count.ShouldBe(51);
        result.StatusPerFileUrl.Values.ShouldAllBe(isOnline => isOnline);
        httpMessageHandler
            .Requests.Select(request =>
                HttpUtility.ParseQueryString(request.Uri.Query).GetValues("ref[]")!.Length
            )
            .ShouldBe([50, 1]);
    }

    [Test]
    public async Task TryLoginAsync_FolderListSucceeds_ReturnsSuccess()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => """{"success":true,"result":[]}""");

        // Act
        var result = await hoster.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        httpMessageHandler.Requests.Single().Uri.AbsolutePath.ShouldBe("/folderlist");
    }

    [Test]
    public async Task TryLoginAsync_InvalidLogin_ReturnsFailureWithMessage()
    {
        // Arrange
        httpMessageHandler.RespondWith(_ => InvalidLoginResponse);

        // Act
        var result = await hoster.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("Invalid login");
    }

    [Test]
    public async Task CreateFolderAsync_FolderWithSameTitleOnSecondPage_ReturnsExistingFolderIdWithoutCreatingFolder()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"] == "1"
                ? """{"success":true,"pages":2,"result":{"folders":[{"id":"1104215","title":"Other.Release"}],"files":[]}}"""
                : """{"success":true,"pages":2,"result":{"folders":[{"id":"1104216","title":"Bearcat.Release"}],"files":[]}}"""
        );

        // Act
        var result = await hoster.CreateFolderAsync(
            "Bearcat.Release",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("1104216");
        httpMessageHandler
            .Requests.Select(request =>
                $"{request.Uri.AbsolutePath}?page={HttpUtility.ParseQueryString(request.Uri.Query)["page"]}"
            )
            .ShouldBe(["/folderlist?page=1", "/folderlist?page=2"]);
    }

    [Test]
    public async Task CreateFolderAsync_EmptyAccountReturnsEmptyResultArray_CreatesFolder()
    {
        // Arrange
        httpMessageHandler.RespondWith(request =>
            request.RequestUri!.AbsolutePath == "/folderlist"
                ? """{"success":true,"result":[]}"""
                : """{"success":true,"result":{"id":"1104217"}}"""
        );

        // Act
        var result = await hoster.CreateFolderAsync(
            "Bearcat.Release",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("1104217");
        httpMessageHandler
            .Requests.Select(request => request.Uri.AbsolutePath)
            .ShouldBe(["/folderlist", "/foldercreate"]);
        HttpUtility
            .ParseQueryString(httpMessageHandler.Requests.Last().Uri.Query)["title"]
            .ShouldBe("Bearcat.Release");
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
