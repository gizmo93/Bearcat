using System.Net;
using System.Net.Http.Headers;
using Bearcat.Hosters.Fichier;
using Bearcat.Hosters.Fichier.Api;
using Bearcat.Hosters.Fichier.Api.Folder;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Fichier;

public class ApiClientTest
{
    private Mock<IFichierApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private TestHttpMessageHandler httpMessageHandler = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IFichierApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpMessageHandler = new TestHttpMessageHandler();
        var loggerMock = new Mock<ILogger<ApiClient>>();

        httpClientFactoryMock
            .Setup(x => x.CreateClient(ApiClient.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        );
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
    }

    [Test]
    public async Task UploadFileAsync_SendsRcloneCompatibleMultipartAndReturnsDownloadLink()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        var capturedUploadBody = string.Empty;
        var capturedUploadContentType = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request.RequestUri!.ToString().ShouldBe(
                "https://api.1fichier.com/v1/upload/get_upload_server.cgi"
            );
            request.Headers.Authorization.ShouldNotBeNull();
            request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
            request.Headers.Authorization.Parameter.ShouldBe("api-key");
            request.Content.ShouldNotBeNull();
            request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");

            return CreateJsonResponse("""{"id":"Upload1234","url":"up1.1fichier.test"}""");
        });

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.ToString().ShouldBe(
                "https://up1.1fichier.test/upload.cgi?id=Upload1234"
            );
            request.Headers.Authorization.ShouldNotBeNull();
            request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
            request.Headers.Authorization.Parameter.ShouldBe("api-key");

            capturedUploadContentType = request.Content!.Headers.ContentType!.ToString();
            capturedUploadBody = await request.Content.ReadAsStringAsync();

            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("/end.pl?xid=Upload1234", UriKind.Relative);
            return response;
        });

        httpMessageHandler.Enqueue(request =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request.RequestUri!.ToString().ShouldBe(
                "https://up1.1fichier.test/end.pl?xid=Upload1234"
            );
            request.Headers.TryGetValues("JSON", out var jsonHeaderValues).ShouldBeTrue();
            jsonHeaderValues!.Single().ShouldBe("1");

            return Task.FromResult(
                CreateJsonResponse(
                    """
                    {
                      "incoming": 0,
                      "links": [
                        {
                          "download": "https://1fichier.com/?abc",
                          "filename": "archive.part01.rar",
                          "size": "14"
                        }
                      ]
                    }
                    """
                )
            );
        });

        await using var stream = new MemoryStream("upload-content"u8.ToArray());

        // Act
        var result = await apiClient.UploadFileAsync(
            config,
            stream,
            "archive.part01.rar",
            null,
            CancellationToken.None
        );

        // Assert
        result.Links.Single().Download.ShouldBe("https://1fichier.com/?abc");
        capturedUploadContentType.ShouldStartWith("multipart/form-data; boundary=");
        capturedUploadContentType.ShouldNotContain("boundary=\"");
        capturedUploadBody.ShouldContain("Content-Disposition: form-data; name=\"did\"\r\n\r\n0");
        capturedUploadBody.ShouldContain(
            "Content-Disposition: form-data; name=\"file[]\"; filename=\"archive.part01.rar\"\r\n\r\nupload-content"
        );
        capturedUploadBody.ShouldNotContain("filename*=");
        capturedUploadBody.ShouldNotContain("Content-Type: application/octet-stream");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task UploadFileAsync_FolderId_SendsFolderIdAsDid()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        var capturedUploadBody = string.Empty;

        SetupSuccessfulUpload(capturedBody => capturedUploadBody = capturedBody);

        await using var stream = new MemoryStream("upload-content"u8.ToArray());

        // Act
        await apiClient.UploadFileAsync(
            config,
            stream,
            "archive.part01.rar",
            "12345",
            CancellationToken.None
        );

        // Assert
        capturedUploadBody.ShouldContain("Content-Disposition: form-data; name=\"did\"\r\n\r\n12345");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolderWithName_ReturnsExistingFolderId()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = "OK",
                    FolderId = 0,
                    SubFolders =
                    [
                        new FolderListResponse.Folder { Id = 222, Name = "other-folder" },
                        new FolderListResponse.Folder { Id = 123, Name = "release-folder" },
                    ],
                }
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("123");
        apiMock.Verify(
            x =>
                x.CreateFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<CreateFolderRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_NoExistingRootFolderWithName_CreatesFolder()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = "OK",
                    FolderId = 0,
                    SubFolders =
                    [
                        new FolderListResponse.Folder { Id = 222, Name = "other-folder" },
                    ],
                }
            );
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    "Bearer api-key",
                    It.Is<CreateFolderRequest>(request =>
                        request.Name == "release-folder" && request.FolderId == 0
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new CreateFolderResponse
                {
                    Status = "OK",
                    FolderId = 123,
                    Name = "release-folder",
                }
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("123");
    }

    private void SetupSuccessfulUpload(Action<string> captureUploadBody)
    {
        httpMessageHandler.Enqueue(_ =>
        {
            return Task.FromResult(CreateJsonResponse("""{"id":"Upload1234","url":"up1.1fichier.test"}"""));
        });

        httpMessageHandler.Enqueue(async request =>
        {
            captureUploadBody(await request.Content!.ReadAsStringAsync());

            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("/end.pl?xid=Upload1234", UriKind.Relative);
            return response;
        });

        httpMessageHandler.Enqueue(_ =>
        {
            return Task.FromResult(
                CreateJsonResponse(
                    """
                    {
                      "incoming": 0,
                      "links": [
                        {
                          "download": "https://1fichier.com/?abc",
                          "filename": "archive.part01.rar",
                          "size": "14"
                        }
                      ]
                    }
                    """
                )
            );
        });
    }

    private static HttpResponseMessage CreateJsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
            },
        };
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> responses = [];

        public int PendingRequests => responses.Count;

        public void Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> response)
        {
            responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            responses.Count.ShouldBeGreaterThan(0);
            return responses.Dequeue()(request);
        }
    }
}
