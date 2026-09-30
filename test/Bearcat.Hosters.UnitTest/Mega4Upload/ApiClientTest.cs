using System.Net;
using System.Text;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Mega4Upload.Api;
using Bearcat.Hosters.Shared;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Mega4Upload;

public class ApiClientTest
{
    [Test]
    public async Task UploadFileAsync_ApiAnswersWithJsonArray_SendsAjaxFieldAndReturnsFileCode()
    {
        // Arrange
        var handler = new RecordingUploadHandler(
            """[{"file_code":"zphzpngmx66a","file_status":"OK"}]"""
        );
        var apiClient = CreateApiClient(handler);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("upload-content"));

        // Act
        var response = await apiClient.UploadFileAsync(
            stream: stream,
            fileName: "archive.part01.rar",
            uploadUrl: "https://s14.mega4down.com/cgi-bin/upload.cgi",
            sessionId: "session-id",
            cancellationToken: CancellationToken.None
        );

        // Assert
        response.FileCode.ShouldBe("zphzpngmx66a");
        response.FileStatus.ShouldBe("OK");
        handler.RequestUri?.ToString().ShouldBe("https://s14.mega4down.com/cgi-bin/upload.cgi");
        handler.RequestBody.ShouldContain("name=\"ajax\"");
        handler.RequestBody.ShouldContain("name=\"sess_id\"");
        handler.RequestBody.ShouldContain("name=\"file\"");
    }

    [Test]
    public async Task FilesExistAsync_ApiAnswersUnauthorized_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var apiClient = CreateRefitApiClient(
            new UnauthorizedHandler(
                """{"msg":"Unauthorized","server_time":"2026-09-30 10:15:03","status":401}"""
            )
        );

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            apiClient.FilesExistAsync(
                "api-key",
                new HashSet<string> { "zphzpngmx66a" },
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("Unauthorized");
    }

    [Test]
    public async Task RequestUploadAsync_ApiAnswersUnauthorized_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var apiClient = CreateRefitApiClient(
            new UnauthorizedHandler(
                """{"msg":"Unauthorized","server_time":"2026-09-30 10:15:03","status":401}"""
            )
        );

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            apiClient.RequestUploadAsync("api-key", CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe("Unauthorized");
    }

    private static ApiClient CreateRefitApiClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ApiClient.ApiBaseUrl) };
        var httpClientProvider = new HttpClientProvider(new Mock<IHttpClientFactory>().Object);

        return new ApiClient(
            RestService.For<IMega4UploadApi>(httpClient),
            httpClientProvider,
            new HosterFileDownloader(httpClientProvider)
        );
    }

    private static ApiClient CreateApiClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(httpClient);
        var httpClientProvider = new HttpClientProvider(httpClientFactoryMock.Object);

        return new ApiClient(
            new Mock<IMega4UploadApi>(MockBehavior.Strict).Object,
            httpClientProvider,
            new HosterFileDownloader(httpClientProvider)
        );
    }

    private sealed class RecordingUploadHandler(string responseContent) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent),
            };
        }
    }

    private sealed class UnauthorizedHandler(string responseContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(responseContent),
                    RequestMessage = request,
                }
            );
        }
    }
}
