using System.Net;
using System.Text;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.FileFactory;
using Bearcat.Hosters.FileFactory.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.FileFactory;

public class ApiClientTest
{
    private const string Email = "bearcat@example.com";

    private const string Password = "filefactory-password";

    private const string FirstToken = "jwt-1";

    private const string SecondToken = "jwt-2";

    private const string UploadSessionId = "upload-session-1";

    private const string UploadUrl =
        "https://uploads-9374983274.s3.us-east-005.backblazeb2.com/bearcat-test.bin?X-Amz-Signature=abc";

    private const string FileId = "x17bs33m3smp0yvfqsbr3jyjxs8japhr";

    private const string OnlineViewhash = "3b42efa34953f497b27e6a9220ea72c5";

    private const string DeletedViewhash = "3b42efa34953f497b27e6a9220ea72c6";

    private const string CompleteUploadResponse =
        """{"success":true,"fileId":"x17bs33m3smp0yvfqsbr3jyjxs8japhr","viewhash":"e91919f6101d3ecaefa7960a62d66f06","downloadUrl":"https://www.filefactory.com/file/e91919f6101d3ecaefa7960a62d66f06"}""";

    private const string UserId = "jd7f2ah3mk9c1xw4q8z6v5n0rbp3t2ye";

    private const string CurrentUserResponse =
        $$$"""{"status":"success","value":{"id":"{{{UserId}}}","email":"bearcat@example.com"}}""";

    private readonly Dictionary<string, string> convexResponsePerPath = [];

    private RecordingHttpMessageHandler httpMessageHandler = null!;
    private ApiClient apiClient = null!;
    private FileFactoryConfig config = null!;

    [SetUp]
    public void SetUp()
    {
        httpMessageHandler = new RecordingHttpMessageHandler();
        convexResponsePerPath.Clear();
        var webApi = RestService.For<IFileFactoryWebApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.WebApiBaseUrl),
            }
        );
        var convexApi = RestService.For<IFileFactoryConvexApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(ApiClient.ConvexApiBaseUrl),
            }
        );
        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        apiClient = new ApiClient(
            webApi: webApi,
            convexApi: convexApi,
            httpClientProvider: new HttpClientProvider(httpClientFactoryMock.Object),
            logger: new Mock<ILogger<ApiClient>>().Object
        )
        {
            MaximumCompleteUploadRetryDelay = TimeSpan.Zero,
        };
        config = new FileFactoryConfig { Email = Email, Password = Password };
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_SignsPutsAndCompletesWithSessionCookie()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupSignUpload();
        SetupPutFile();
        SetupCompleteUpload(() => CreateJsonResponse(HttpStatusCode.OK, CompleteUploadResponse));

        // Act
        var result = await UploadAsync(folderId: "folder-1");

        // Assert
        result.FileId.ShouldBe(FileId);
        result.FileUrl.ShouldBe(
            "https://www.filefactory.com/file/e91919f6101d3ecaefa7960a62d66f06"
        );

        var loginRequest = httpMessageHandler.SingleRequest("/api/auth");
        loginRequest.Headers["Origin"].ShouldBe("https://www.filefactory.com");
        loginRequest.Body.ShouldBe(
            """{"action":"auth:signIn","args":{"provider":"password","params":{"email":"bearcat@example.com","password":"filefactory-password","flow":"signIn"}}}"""
        );

        var signRequest = httpMessageHandler.SingleRequest("/api/upload/r2/sign");
        signRequest.Headers["Cookie"].ShouldBe($"__Host-__convexAuthJWT={FirstToken}");
        signRequest.Body.ShouldBe(
            """{"useSession":true,"filename":"archive.part1.rar","contentType":"application/octet-stream","size":12,"folderId":"folder-1"}"""
        );

        var putRequest = httpMessageHandler.SingleRequest("/bearcat-test.bin");
        putRequest.Method.ShouldBe(HttpMethod.Put);
        putRequest.Uri.ShouldBe(new Uri(UploadUrl));
        putRequest.ContentType.ShouldBe("application/octet-stream");
        putRequest.Headers.ShouldNotContainKey("Cookie");
        putRequest.Body.ShouldBe("file content");

        var completeRequest = httpMessageHandler.SingleRequest("/api/upload/r2/complete");
        completeRequest.Headers["Cookie"].ShouldBe($"__Host-__convexAuthJWT={FirstToken}");
        completeRequest.Body.ShouldBe($$"""{"sessionId":"{{UploadSessionId}}"}""");
    }

    [Test]
    public async Task UploadFileAsync_LoginReturnsBadRequest_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        httpMessageHandler.Respond(
            "/api/auth",
            _ =>
                CreateJsonResponse(
                    HttpStatusCode.BadRequest,
                    """{"error":"[Request ID: 5f1c2a] Server Error"}"""
                )
        );

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            UploadAsync(folderId: null)
        );

        // Assert
        exception.Message.ShouldContain("Server Error");
        httpMessageHandler.CountRequests("/api/upload/r2/sign").ShouldBe(0);
    }

    [Test]
    public async Task UploadFileAsync_SessionCookieRejected_LogsInAgainAndRetriesWithNewToken()
    {
        // Arrange
        SetupLogin(FirstToken, SecondToken);
        var signResponses = new Queue<Func<HttpResponseMessage>>([
            () => CreateJsonResponse(HttpStatusCode.Unauthorized, """{"error":"Unauthorized"}"""),
            CreateSignUploadResponse,
        ]);
        httpMessageHandler.Respond("/api/upload/r2/sign", _ => signResponses.Dequeue()());
        SetupPutFile();
        SetupCompleteUpload(() => CreateJsonResponse(HttpStatusCode.OK, CompleteUploadResponse));

        // Act
        await UploadAsync(folderId: null);

        // Assert
        httpMessageHandler.CountRequests("/api/auth").ShouldBe(2);
        httpMessageHandler
            .Requests("/api/upload/r2/sign")
            .Select(request => request.Headers["Cookie"])
            .ShouldBe([
                $"__Host-__convexAuthJWT={FirstToken}",
                $"__Host-__convexAuthJWT={SecondToken}",
            ]);
        httpMessageHandler
            .SingleRequest("/api/upload/r2/complete")
            .Headers["Cookie"]
            .ShouldBe($"__Host-__convexAuthJWT={SecondToken}");
    }

    [Test]
    public async Task UploadFileAsync_CompleteFailsWithServerErrorTwice_RetriesUntilSuccess()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupSignUpload();
        SetupPutFile();
        var completeResponses = new Queue<Func<HttpResponseMessage>>([
            () =>
                CreateJsonResponse(
                    HttpStatusCode.ServiceUnavailable,
                    """{"error":"Service Unavailable"}"""
                ),
            () => CreateJsonResponse(HttpStatusCode.TooManyRequests, """{"error":"Too Many"}"""),
            () => CreateJsonResponse(HttpStatusCode.OK, CompleteUploadResponse),
        ]);
        SetupCompleteUpload(() => completeResponses.Dequeue()());

        // Act
        var result = await UploadAsync(folderId: null);

        // Assert
        result.FileId.ShouldBe(FileId);
        httpMessageHandler.CountRequests("/api/upload/r2/complete").ShouldBe(3);
        httpMessageHandler.CountRequests("/bearcat-test.bin").ShouldBe(1);
    }

    [Test]
    public async Task UploadFileAsync_CompleteFailsWithBadRequest_ThrowsWithoutRetrying()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupSignUpload();
        SetupPutFile();
        SetupCompleteUpload(() =>
            CreateJsonResponse(HttpStatusCode.BadRequest, """{"error":"Invalid session"}""")
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            UploadAsync(folderId: null)
        );

        // Assert
        exception.Message.ShouldContain("Invalid session");
        httpMessageHandler.CountRequests("/api/upload/r2/complete").ShouldBe(1);
    }

    [Test]
    public async Task GetFileInfoPerFileIdAsync_ValidAndDeletedLinks_MapsOnlineWithFileNameAndOffline()
    {
        // Arrange
        SetupLogin(FirstToken);
        httpMessageHandler.Respond(
            "/api/tools/link-checker",
            _ =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    $$"""{"results":[{"fileDetails":{"dateAdded":"11/16/2026","downloadUrl":"https://www.filefactory.com/file/{{OnlineViewhash}}/","name":"bearcat-test.txt","size":"49 B","viewhash":"{{OnlineViewhash}}"},"importEligible":"owned","originalUrl":"https://www.filefactory.com/file/{{OnlineViewhash}}","status":"valid"},{"errorMessage":"File not found or has been deleted.","originalUrl":"https://www.filefactory.com/file/{{DeletedViewhash}}","status":"invalid"}]}"""
                )
        );

        // Act
        var result = await apiClient.GetFileInfoPerFileIdAsync(
            config: config,
            fileIds: [OnlineViewhash, DeletedViewhash],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result[OnlineViewhash].ShouldBe(new(IsOnline: true, "bearcat-test.txt", SizeBytes: null));
        result[DeletedViewhash].ShouldBe(new(IsOnline: false, FileName: null, SizeBytes: null));

        var linkCheckRequest = httpMessageHandler.SingleRequest("/api/tools/link-checker");
        linkCheckRequest.Headers["Cookie"].ShouldBe($"__Host-__convexAuthJWT={FirstToken}");
        ReadLinks(linkCheckRequest)
            .ShouldBe([
                $"https://www.filefactory.com/file/{OnlineViewhash}",
                $"https://www.filefactory.com/file/{DeletedViewhash}",
            ]);
    }

    [TestCase(
        """[{"errorMessage":"Temporarily unavailable.","originalUrl":"{0}","status":"invalid"}]"""
    )]
    [TestCase("""[{"originalUrl":"{0}","status":"pending"}]""")]
    [TestCase("[]")]
    public async Task GetFileInfoPerFileIdAsync_UnknownOrMissingResult_Throws(
        string resultsTemplate
    )
    {
        // Arrange
        SetupLogin(FirstToken);
        var results = resultsTemplate.Replace(
            "{0}",
            $"https://www.filefactory.com/file/{DeletedViewhash}",
            StringComparison.Ordinal
        );
        httpMessageHandler.Respond(
            "/api/tools/link-checker",
            _ => CreateJsonResponse(HttpStatusCode.OK, $$"""{"results":{{results}}}""")
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.GetFileInfoPerFileIdAsync(config, [DeletedViewhash], CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain(DeletedViewhash);
    }

    [Test]
    public async Task GetFileInfoPerFileIdAsync_RateLimited_ThrowsWithRetryAfter()
    {
        // Arrange
        SetupLogin(FirstToken);
        httpMessageHandler.Respond(
            "/api/tools/link-checker",
            _ =>
                CreateJsonResponse(
                    HttpStatusCode.TooManyRequests,
                    """{"error":"Too many requests","retryAfter":42}"""
                )
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.GetFileInfoPerFileIdAsync(config, [OnlineViewhash], CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("retry after 42 seconds");
    }

    [Test]
    public async Task GetFileInfoPerFileIdAsync_HundredAndOneFileIds_SendsAtMostHundredLinksPerRequest()
    {
        // Arrange
        SetupLogin(FirstToken);
        var fileIds = Enumerable.Range(1, 101).Select(index => $"{index:x32}").ToList();
        httpMessageHandler.Respond(
            "/api/tools/link-checker",
            request =>
            {
                var results = ReadLinks(request)
                    .Select(link => new { originalUrl = link, status = "valid" })
                    .ToList();

                return CreateJsonResponse(
                    HttpStatusCode.OK,
                    JsonSerializer.Serialize(new { results })
                );
            }
        );

        // Act
        var result = await apiClient.GetFileInfoPerFileIdAsync(
            config: config,
            fileIds: fileIds,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Count.ShouldBe(101);
        httpMessageHandler
            .Requests("/api/tools/link-checker")
            .Select(request => ReadLinks(request).Count)
            .ShouldBe([100, 1]);
    }

    [Test]
    public async Task EnsureCredentialsAcceptedAsync_CurrentUserIsNullAfterRelogin_Throws()
    {
        // Arrange
        SetupLogin(FirstToken, SecondToken);
        httpMessageHandler.Respond(
            "/api/query",
            _ => CreateJsonResponse(HttpStatusCode.OK, """{"status":"success","value":null}""")
        );

        // Act
        await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.EnsureCredentialsAcceptedAsync(config, CancellationToken.None)
        );

        // Assert
        httpMessageHandler.CountRequests("/api/auth").ShouldBe(2);
        var queryRequests = httpMessageHandler.Requests("/api/query");
        queryRequests
            .Select(request => request.Headers["Authorization"])
            .ShouldBe([$"Bearer {FirstToken}", $"Bearer {SecondToken}"]);
        queryRequests[0].Body.ShouldBe("""{"path":"users:me","args":{},"format":"json"}""");
    }

    [Test]
    public async Task CreateFolderAsync_RootFolderWithSameNameExists_ReturnsItsIdWithoutCreating()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupConvexResponse("users:me", CurrentUserResponse);
        SetupConvexResponse(
            "file_manager/folders:getFolderSelectOptions",
            """{"status":"success","value":[{"hash":"bcparentmwjvx","id":"parent-folder-id","level":0.0,"name":"parent"},{"hash":"bcsubmwjvxb6u","id":"subfolder-id","level":1.0,"name":"bearcat-test"},{"hash":"bctestmwjvxb6u","id":"root-folder-id","level":0.0,"name":"bearcat-test"}]}"""
        );

        // Act
        var folderId = await apiClient.CreateFolderAsync(
            config,
            "bearcat-test",
            CancellationToken.None
        );

        // Assert
        folderId.ShouldBe("root-folder-id");
        httpMessageHandler
            .Requests("/api/query")
            .Select(request => request.Body)
            .ShouldBe([
                """{"path":"users:me","args":{},"format":"json"}""",
                $$"""{"path":"file_manager/folders:getFolderSelectOptions","args":{"userId":"{{UserId}}"},"format":"json"}""",
            ]);
        httpMessageHandler.CountRequests("/api/mutation").ShouldBe(0);
    }

    [Test]
    public async Task CreateFolderAsync_OnlySubfolderWithSameNameExists_CreatesRootFolderWithRandomHash()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupConvexResponse("users:me", CurrentUserResponse);
        SetupConvexResponse(
            "file_manager/folders:getFolderSelectOptions",
            """{"status":"success","value":[{"hash":"bcsubmwjvxb6u","id":"subfolder-id","level":1.0,"name":"bearcat-test"}]}"""
        );
        SetupConvexResponse(
            "file_manager/folders:createFolder",
            """{"status":"success","value":"new-folder-id"}"""
        );

        // Act
        var folderId = await apiClient.CreateFolderAsync(
            config,
            "bearcat-test",
            CancellationToken.None
        );

        // Assert
        folderId.ShouldBe("new-folder-id");

        var createRequest = httpMessageHandler.SingleRequest("/api/mutation");
        createRequest.Headers["Authorization"].ShouldBe($"Bearer {FirstToken}");
        using var body = JsonDocument.Parse(createRequest.Body);
        var path = body.RootElement.GetProperty("path").GetString();
        var arguments = body.RootElement.GetProperty("args");
        var hash = arguments.GetProperty("hash").GetString()!;

        path.ShouldBe("file_manager/folders:createFolder");
        arguments.EnumerateObject().Select(property => property.Name).ShouldBe(["name", "hash"]);
        arguments.GetProperty("name").GetString().ShouldBe("bearcat-test");
        hash.ShouldMatch("^[0-9a-ik-z][0-9a-z]{12}$");
    }

    [Test]
    public async Task MoveFilesToFolderAsync_TwoFiles_SendsUserIdFileIdsAndTargetFolderInOneMutation()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupConvexResponse("users:me", CurrentUserResponse);
        SetupConvexResponse(
            "file_manager/files:moveFiles",
            """{"status":"success","value":null}"""
        );

        // Act
        await apiClient.MoveFilesToFolderAsync(
            config: config,
            fileIds: [FileId, "x27bs33m3smp0yvfqsbr3jyjxs8japhr"],
            folderId: "root-folder-id",
            cancellationToken: CancellationToken.None
        );

        // Assert
        httpMessageHandler
            .SingleRequest("/api/mutation")
            .Body.ShouldBe(
                $$"""{"path":"file_manager/files:moveFiles","args":{"userId":"{{UserId}}","fileIds":["{{FileId}}","x27bs33m3smp0yvfqsbr3jyjxs8japhr"],"targetFolderId":"root-folder-id"},"format":"json"}"""
            );
    }

    [Test]
    public async Task MoveFilesToFolderAsync_ConvexReturnsErrorStatus_ThrowsWithErrorMessage()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupConvexResponse("users:me", CurrentUserResponse);
        SetupConvexResponse(
            "file_manager/files:moveFiles",
            """{"status":"error","errorMessage":"[Request ID: 5f1c2a] Server Error"}"""
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.MoveFilesToFolderAsync(
                config: config,
                fileIds: [FileId],
                folderId: "unknown-folder-id",
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("[Request ID: 5f1c2a] Server Error");
    }

    [Test]
    public async Task GetFolderContentStatesAsync_RootFolders_MapsFilePagesAndOmitsUnlistedFolders()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupConvexResponse("users:me", CurrentUserResponse);
        SetupConvexResponse(
            "file_manager/folders:getFolderSelectOptions",
            """{"status":"success","value":[{"hash":"bcsubmwjvxb6u","id":"empty-folder-id","level":0.0,"name":"Empty.Release"},{"hash":"bcsubmwjvxb6v","id":"files-folder-id","level":0.0,"name":"Files.Release"}]}"""
        );
        var filePagePerFolderId = new Dictionary<string, string>
        {
            ["empty-folder-id"] =
                """{"status":"success","value":{"page":[],"isDone":true,"continueCursor":"cursor-1"}}""",
            ["files-folder-id"] =
                $$$"""{"status":"success","value":{"page":[{"_id":"{{{FileId}}}","fname":"archive.part1.rar","folderId":"files-folder-id","statushash":"active"}],"isDone":false,"continueCursor":"cursor-2"}}""",
        };
        httpMessageHandler.Respond(
            "/api/query",
            request =>
                RespondToFileListRequest(request: request, filePagePerFolderId: filePagePerFolderId)
        );

        // Act
        var result = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds: ["empty-folder-id", "files-folder-id", "unlisted-folder-id"],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new HosterFolderContentStateDto("empty-folder-id", HosterFolderContentState.Empty),
            new HosterFolderContentStateDto("files-folder-id", HosterFolderContentState.NotEmpty),
        ]);
        httpMessageHandler
            .Requests("/api/query")
            .Select(request => request.Body)
            .ShouldContain(
                $$$"""{"path":"file_manager/files:listFiles","args":{"userId":"{{{UserId}}}","folderId":"empty-folder-id","paginationOpts":{"numItems":1,"cursor":null}},"format":"json"}"""
            );
    }

    [Test]
    public async Task GetFolderContentStatesAsync_AccountContainsSubfolder_ReturnsNoStatesWithoutListingFiles()
    {
        // Arrange
        SetupLogin(FirstToken);
        SetupConvexResponse("users:me", CurrentUserResponse);
        SetupConvexResponse(
            "file_manager/folders:getFolderSelectOptions",
            """{"status":"success","value":[{"hash":"bcsubmwjvxb6u","id":"empty-folder-id","level":0.0,"name":"Empty.Release"},{"hash":"bcsubmwjvxb6w","id":"subfolder-id","level":1.0,"name":"Sub"}]}"""
        );

        // Act
        var result = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds: ["empty-folder-id"],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
        httpMessageHandler.CountRequests("/api/query").ShouldBe(2);
    }

    private void SetupLogin(params string[] tokens)
    {
        var tokenQueue = new Queue<string>(tokens);
        httpMessageHandler.Respond(
            "/api/auth",
            _ =>
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    $$$"""{"tokens":{"token":"{{{tokenQueue.Dequeue()}}}","refreshToken":"dummy"}}"""
                )
        );
    }

    private void SetupConvexResponse(string convexPath, string json)
    {
        convexResponsePerPath[convexPath] = json;
        httpMessageHandler.Respond("/api/query", RespondToConvexRequest);
        httpMessageHandler.Respond("/api/mutation", RespondToConvexRequest);
    }

    private HttpResponseMessage RespondToConvexRequest(RecordedRequest request)
    {
        using var body = JsonDocument.Parse(request.Body);
        var convexPath = body.RootElement.GetProperty("path").GetString()!;

        return CreateJsonResponse(HttpStatusCode.OK, convexResponsePerPath[convexPath]);
    }

    private HttpResponseMessage RespondToFileListRequest(
        RecordedRequest request,
        Dictionary<string, string> filePagePerFolderId
    )
    {
        using var body = JsonDocument.Parse(request.Body);

        if (body.RootElement.GetProperty("path").GetString() != "file_manager/files:listFiles")
        {
            return RespondToConvexRequest(request);
        }

        var folderId = body.RootElement.GetProperty("args").GetProperty("folderId").GetString()!;

        return CreateJsonResponse(HttpStatusCode.OK, filePagePerFolderId[folderId]);
    }

    private void SetupSignUpload()
    {
        httpMessageHandler.Respond("/api/upload/r2/sign", _ => CreateSignUploadResponse());
    }

    private void SetupPutFile()
    {
        httpMessageHandler.Respond(
            "/bearcat-test.bin",
            _ => new HttpResponseMessage(HttpStatusCode.OK)
        );
    }

    private void SetupCompleteUpload(Func<HttpResponseMessage> responder)
    {
        httpMessageHandler.Respond("/api/upload/r2/complete", _ => responder());
    }

    private static HttpResponseMessage CreateSignUploadResponse()
    {
        return CreateJsonResponse(
            HttpStatusCode.OK,
            $$"""{"url":"{{UploadUrl}}","key":"uploads/bearcat-test.bin","filename":"archive.part1.rar","fileId":"{{FileId}}","sessionId":"{{UploadSessionId}}"}"""
        );
    }

    private async Task<FileFactoryUploadResult> UploadAsync(string? folderId)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("file content"));

        return await apiClient.UploadFileAsync(
            config: config,
            stream: stream,
            fileName: "archive.part1.rar",
            fileSize: stream.Length,
            folderId: folderId,
            cancellationToken: CancellationToken.None
        );
    }

    private static List<string> ReadLinks(RecordedRequest request)
    {
        using var body = JsonDocument.Parse(request.Body);

        return body.RootElement.GetProperty("links").GetString()!.Split('\n').ToList();
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        IReadOnlyDictionary<string, string> Headers,
        string? ContentType,
        string Body
    );

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<
            string,
            Func<RecordedRequest, HttpResponseMessage>
        > responders = [];

        private readonly List<RecordedRequest> requests = [];

        public void Respond(string path, Func<RecordedRequest, HttpResponseMessage> responder)
        {
            responders[path] = responder;
        }

        public RecordedRequest SingleRequest(string path)
        {
            return requests.Single(request => request.Uri.AbsolutePath == path);
        }

        public List<RecordedRequest> Requests(string path)
        {
            return requests.Where(request => request.Uri.AbsolutePath == path).ToList();
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

            var recordedRequest = new RecordedRequest(
                Method: request.Method,
                Uri: request.RequestUri!,
                Headers: headers,
                ContentType: request.Content?.Headers.ContentType?.MediaType,
                Body: body
            );
            requests.Add(recordedRequest);

            var response = responders[request.RequestUri!.AbsolutePath](recordedRequest);
            response.RequestMessage = request;

            return response;
        }
    }
}
