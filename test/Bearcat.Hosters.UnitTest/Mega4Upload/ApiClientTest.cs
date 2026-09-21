using System.Net;
using System.Text;
using Bearcat.Hosters.Mega4Upload.Api;
using Bearcat.Hosters.Shared;
using Moq;
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
}
