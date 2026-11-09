using System.Net;
using System.Text;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Filer;
using Bearcat.Hosters.Filer.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Filer;

public class ApiClientTest
{
    private const string Email = "bearcat@example.com";

    private const string Password = "filer-password";

    private const string CsrfToken = "csrf-token-1";

    private const string AnonymousSessionCookie = "anonymous-jwt";

    private const string LoggedInSessionCookie = "logged-in-jwt";

    private const string UploadSession = "upload-session-1";

    private const string FileHash = "aa0ea9176b7b5b95";

    private const string EditCode = "d8f65339142f346a";

    private const string FolderHash = "bf939f014bcd87b4";

    private const string UploadResponseTemplate = """
        <html><head><script type="text/javascript">document.domain = "filer.net";</script></head><body>[{"name":"archive.part1.rar","hash":"aa0ea9176b7b5b95","edit":"d8f65339142f346a","error":{0}}]</body></html>
        """;

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private ApiClient apiClient = null!;
    private FilerConfig config = null!;
    private string targetFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();
        var api = RestService.For<IFilerApi>(
            new HttpClient(httpMessageHandler) { BaseAddress = new Uri(ApiClient.ApiBaseUrl) }
        );
        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.DownloadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));
        var httpClientProvider = new HttpClientProvider(httpClientFactoryMock.Object);

        apiClient = new ApiClient(
            api,
            httpClientProvider,
            new HosterFileDownloader(httpClientProvider),
            new Mock<ILogger<ApiClient>>().Object
        );
        config = new FilerConfig { Email = Email, Password = Password };
        targetFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
        File.Delete(targetFilePath);
    }

    [Test]
    public async Task UploadFileAsync_NotLoggedIn_LogsInWithCsrfTokenOriginAndCookieAndUsesNewSessionCookie()
    {
        // Arrange
        SetupLogin();
        SetupUploadServer(userId: "732579");
        SetupUploadResponse("false");

        // Act
        await UploadAsync();

        // Assert
        var loginRequest = httpMessageHandler.SingleRequest("/api/user/login");
        loginRequest.Headers["X-CSRF-Token"].ShouldBe(CsrfToken);
        loginRequest.Headers["Origin"].ShouldBe("https://filer.net");
        loginRequest.Headers["Cookie"].ShouldBe($"jwt={AnonymousSessionCookie}");
        loginRequest.Body.ShouldContain($"\"email\":\"{Email}\"");
        httpMessageHandler
            .SingleRequest("/api/upload/server")
            .Headers["Cookie"]
            .ShouldBe($"jwt={LoggedInSessionCookie}");
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_PostsMultipartToUploadServerAndReturnsFileLink()
    {
        // Arrange
        SetupLogin();
        SetupUploadServer(userId: "732579");
        SetupUploadResponse("false");

        // Act
        var result = await UploadAsync();

        // Assert
        result.FileHash.ShouldBe("aa0ea9176b7b5b95");
        result.FileUrl.ShouldBe("https://filer.net/get/aa0ea9176b7b5b95");
        var uploadRequest = httpMessageHandler.SingleRequest("/upload");
        uploadRequest.Uri.Host.ShouldBe("dl35.filer.net");
        uploadRequest.Body.ShouldContain("name=session\r\n\r\nupload-session-1");
        uploadRequest.Body.ShouldContain("name=user_id\r\n\r\n732579");
        uploadRequest.Body.ShouldContain("name=files; filename=archive.part1.rar");
        uploadRequest.Body.ShouldContain("file content");
    }

    [TestCase("1")]
    [TestCase("true")]
    public async Task UploadFileAsync_UploadResponseReportsError_Throws(string errorValue)
    {
        // Arrange
        SetupLogin();
        SetupUploadServer(userId: "732579");
        SetupUploadResponse(errorValue);

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(UploadAsync);

        // Assert
        exception.Message.ShouldBe($"Filer upload failed with code {errorValue}");
    }

    [Test]
    public async Task UploadFileAsync_UploadServerSessionIsAnonymous_LogsInAgainAndRetriesOnce()
    {
        // Arrange
        SetupLogin();
        var uploadServerResponses = new Queue<string>(["null", "732579"]);
        httpMessageHandler.Respond(
            "/api/upload/server",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    $$"""{"server":"dl35.filer.net","session":"{{UploadSession}}","user_id":{{uploadServerResponses.Dequeue()}}}"""
                )
        );
        SetupUploadResponse("false");

        // Act
        await UploadAsync();

        // Assert
        httpMessageHandler.CountRequests("/api/user/login").ShouldBe(2);
        httpMessageHandler.CountRequests("/api/upload/server").ShouldBe(2);
        httpMessageHandler.CountRequests("/upload").ShouldBe(1);
    }

    [Test]
    public async Task UploadFileAsync_InvalidCredentials_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        SetupCsrfToken();
        httpMessageHandler.Respond(
            "/api/user/login",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.Unauthorized,
                    """{"message":"INVALID_CREDENTIALS"}"""
                )
        );

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(UploadAsync);

        // Assert
        exception.Message.ShouldContain("INVALID_CREDENTIALS");
        httpMessageHandler.CountRequests("/upload").ShouldBe(0);
    }

    [Test]
    public async Task GetFileInfoPerFileHashAsync_OneHashOnlineAndOneMissing_MapsMissingHashAsOffline()
    {
        // Arrange
        httpMessageHandler.Respond(
            "/api/multi_status/aa0ea9176b7b5b95%7C36293ad21cebb03b.json",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    """{"code":200,"status":"success","data":{"aa0ea9176b7b5b95":{"file_hash":"aa0ea9176b7b5b95","file_name":"archive.part1.rar","file_size":1048576,"premium_only":false,"view_count":1,"md5":"3d39ed1608322dbd0b9b9f4fa1493f2c"}}}"""
                )
        );

        // Act
        var result = await apiClient.GetFileInfoPerFileHashAsync(
            ["aa0ea9176b7b5b95", "36293ad21cebb03b"],
            CancellationToken.None
        );

        // Assert
        result["aa0ea9176b7b5b95"].IsOnline.ShouldBeTrue();
        result["aa0ea9176b7b5b95"].FileName.ShouldBe("archive.part1.rar");
        result["aa0ea9176b7b5b95"].SizeBytes.ShouldBe(1048576);
        result["36293ad21cebb03b"].IsOnline.ShouldBeFalse();
    }

    [Test]
    public async Task GetFileInfoPerFileHashAsync_SixHashes_RequestsAtMostFiveHashesPerRequest()
    {
        // Arrange
        const string emptyStatusResponse = """{"code":200,"status":"success","data":{}}""";

        httpMessageHandler.Respond(
            "/api/multi_status/0000000000000001%7C0000000000000002%7C0000000000000003%7C0000000000000004%7C0000000000000005.json",
            () => CreateJsonResponse(HttpStatusCode.OK, emptyStatusResponse)
        );

        httpMessageHandler.Respond(
            "/api/multi_status/0000000000000006.json",
            () => CreateJsonResponse(HttpStatusCode.OK, emptyStatusResponse)
        );

        // Act
        var result = await apiClient.GetFileInfoPerFileHashAsync(
            [
                "0000000000000001",
                "0000000000000002",
                "0000000000000003",
                "0000000000000004",
                "0000000000000005",
                "0000000000000006",
            ],
            CancellationToken.None
        );

        // Assert
        result.Count.ShouldBe(6);
        result.Values.ShouldAllBe(fileInfo => !fileInfo.IsOnline);
    }

    [Test]
    public async Task GetFileInfoPerFileHashAsync_UnexpectedResponse_Throws()
    {
        // Arrange
        httpMessageHandler.Respond(
            "/api/multi_status/aa0ea9176b7b5b95.json",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.InternalServerError,
                    """{"code":505,"status":"file not found","data":{}}"""
                )
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.GetFileInfoPerFileHashAsync(["aa0ea9176b7b5b95"], CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("InternalServerError");
    }

    [Test]
    public async Task EnsureCredentialsAcceptedAsync_WrongPassword_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        httpMessageHandler.Respond(
            "/api/profile.json",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.Unauthorized,
                    """{"code":401,"status":"invalid credentials","data":{}}"""
                )
        );

        // Act
        await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            apiClient.EnsureCredentialsAcceptedAsync(config, CancellationToken.None)
        );

        // Assert
        httpMessageHandler
            .SingleRequest("/api/profile.json")
            .Headers["Authorization"]
            .ShouldBe(
                $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Password}"))}"
            );
    }

    [Test]
    public async Task UploadFileAsync_WithFolder_MovesUploadedFileByHashWithItsEditCode()
    {
        // Arrange
        SetupLogin();
        SetupUploadServer(userId: "732579");
        SetupUploadResponse("false");
        SetupMoveResponse(movedFiles: 1);

        // Act
        var result = await UploadAsync(folderHash: FolderHash);

        // Assert
        result.EditCode.ShouldBe(EditCode);
        var moveRequest = httpMessageHandler.SingleRequest("/api/user/batch/move");
        moveRequest.Headers["Origin"].ShouldBe("https://filer.net");
        moveRequest.Headers["Cookie"].ShouldBe($"jwt={LoggedInSessionCookie}");
        moveRequest.Body.ShouldBe(
            $$$"""{"files":["{{{FileHash}}}"],"targetFolder":"{{{FolderHash}}}","editCodes":{"{{{FileHash}}}":"{{{EditCode}}}"}}"""
        );
    }

    [Test]
    public async Task MoveFileToFolderAsync_NoFileMoved_Throws()
    {
        // Arrange
        SetupLogin();
        SetupMoveResponse(movedFiles: 0);

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.MoveFileToFolderAsync(
                config: config,
                fileHash: FileHash,
                editCode: EditCode,
                folderHash: FolderHash,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("moved 0 of 1 files");
    }

    [Test]
    public async Task CreateFolderAsync_FolderAlreadyExists_ResolvesHashOfExactNameViaSearch()
    {
        // Arrange
        SetupLogin();
        httpMessageHandler.Respond(
            "/api/user/folders",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.Conflict,
                    """{"message":"Folder with this name already exists"}"""
                )
        );
        httpMessageHandler.Respond(
            "/api/user/folders/search",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    $$"""{"folders":[{"hash":"0000000000000001","name":"Release.Name_UploadId_7.copy","fileCount":0},{"hash":"{{FolderHash}}","name":"Release.Name_UploadId_7","fileCount":0}],"total":2}"""
                )
        );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "Release.Name_UploadId_7",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(FolderHash);
        var createRequest = httpMessageHandler.SingleRequest("/api/user/folders");
        createRequest.Headers["Origin"].ShouldBe("https://filer.net");
        createRequest.Body.ShouldBe("""{"name":"Release.Name_UploadId_7","parentPath":""}""");
        httpMessageHandler
            .SingleRequest("/api/user/folders/search")
            .Uri.Query.ShouldBe("?q=Release.Name_UploadId_7");
    }

    [Test]
    public async Task DownloadFileAsync_FreeAccount_ThrowsPremiumRequired()
    {
        // Arrange
        httpMessageHandler.Respond(
            $"/api/dl/{FileHash}.json",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.Forbidden,
                    """{"code":403,"status":"premium required","data":{}}"""
                )
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(DownloadAsync);

        // Assert
        exception.Message.ShouldBe("Filer download requires a premium account: premium required");
        File.Exists(targetFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task DownloadFileAsync_RedirectBodyWithDownloadUrl_DownloadsFileFromDownloadUrl()
    {
        // Arrange
        httpMessageHandler.Respond(
            $"/api/dl/{FileHash}.json",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.Redirect,
                    $$$"""{"code":302,"status":"redirect","data":{"download_url":"https://server.example.com/download/token123","file_hash":"{{{FileHash}}}","file_name":"archive.part1.rar","file_size":12,"md5":"9e107d9d372bb6826bd81d3542a419d6"}}"""
                )
        );
        httpMessageHandler.Respond(
            "/download/token123",
            () =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("file content"),
                }
        );

        // Act
        await DownloadAsync();

        // Assert
        (await File.ReadAllTextAsync(targetFilePath)).ShouldBe("file content");
        httpMessageHandler
            .SingleRequest($"/api/dl/{FileHash}.json")
            .Headers["Authorization"]
            .ShouldStartWith("Basic ");
    }

    [Test]
    public async Task DownloadFileAsync_FileNotFoundCode_ThrowsHosterFileNotFound()
    {
        // Arrange
        httpMessageHandler.Respond(
            $"/api/dl/{FileHash}.json",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.InternalServerError,
                    """{"code":505,"status":"file not found","data":{}}"""
                )
        );

        // Act
        var exception = await Should.ThrowAsync<HosterFileNotFoundException>(DownloadAsync);

        // Assert
        exception.Message.ShouldContain("file not found");
    }

    private void SetupMoveResponse(int movedFiles)
    {
        httpMessageHandler.Respond(
            "/api/user/batch/move",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    $$"""{"success":true,"message":"Successfully moved {{movedFiles}} files and 0 folders","movedFiles":{{movedFiles}},"movedFolders":0}"""
                )
        );
    }

    private void SetupLogin()
    {
        SetupCsrfToken();
        httpMessageHandler.Respond(
            "/api/user/login",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    """{"message":"Login successful","returnTo":""}""",
                    LoggedInSessionCookie
                )
        );
    }

    private void SetupCsrfToken()
    {
        httpMessageHandler.Respond(
            "/api/app/csrf",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    $$"""{"csrfToken":"{{CsrfToken}}"}""",
                    AnonymousSessionCookie
                )
        );
    }

    private void SetupUploadServer(string userId)
    {
        httpMessageHandler.Respond(
            "/api/upload/server",
            () =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    $$"""{"server":"dl35.filer.net","session":"{{UploadSession}}","user_id":{{userId}}}"""
                )
        );
    }

    private void SetupUploadResponse(string errorValue)
    {
        httpMessageHandler.Respond(
            "/upload",
            () =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        UploadResponseTemplate.Replace("{0}", errorValue, StringComparison.Ordinal),
                        Encoding.UTF8,
                        "text/html"
                    ),
                }
        );
    }

    private async Task<FilerUploadResult> UploadAsync()
    {
        return await UploadAsync(folderHash: null);
    }

    private async Task<FilerUploadResult> UploadAsync(string? folderHash)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("file content"));

        return await apiClient.UploadFileAsync(
            config: config,
            stream: stream,
            fileName: "archive.part1.rar",
            fileSize: stream.Length,
            folderHash: folderHash,
            cancellationToken: CancellationToken.None
        );
    }

    private Task DownloadAsync()
    {
        return apiClient.DownloadFileAsync(
            config: config,
            fileHash: FileHash,
            targetFilePath: targetFilePath,
            progress: NullTransferProgress.Instance,
            expectedSizeBytes: null,
            cancellationToken: CancellationToken.None
        );
    }

    private static HttpResponseMessage CreateJsonResponse(
        HttpStatusCode statusCode,
        string json,
        string? sessionCookie = null
    )
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        if (sessionCookie is not null)
        {
            response.Headers.Add(
                "Set-Cookie",
                $"jwt={sessionCookie}; Max-Age=604800; Domain=filer.net; Path=/; HttpOnly; Secure; SameSite=Lax"
            );
        }

        return response;
    }

    private sealed record RecordedRequest(
        Uri Uri,
        IReadOnlyDictionary<string, string> Headers,
        string Body
    );

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> responders = [];

        private readonly List<RecordedRequest> requests = [];

        public void Respond(string path, Func<HttpResponseMessage> responder)
        {
            responders[path] = responder;
        }

        public RecordedRequest SingleRequest(string path)
        {
            return requests.Single(request => request.Uri.AbsolutePath == path);
        }

        public int CountRequests(string path)
        {
            return requests.Count(request => request.Uri.AbsolutePath == path);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var headers = request.Headers.ToDictionary(
                header => header.Key,
                header => string.Join("; ", header.Value)
            );

            requests.Add(new RecordedRequest(request.RequestUri!, headers, body));

            var response = responders[request.RequestUri!.AbsolutePath]();
            response.RequestMessage = request;

            return response;
        }
    }
}
