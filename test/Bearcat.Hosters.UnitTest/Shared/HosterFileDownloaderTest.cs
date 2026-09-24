using System.Net;
using System.Text;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Shared;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Shared;

public class HosterFileDownloaderTest
{
    private string targetFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        targetFilePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.rar");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(targetFilePath))
        {
            File.Delete(targetFilePath);
        }
    }

    [Test]
    public async Task DownloadToFileAsync_ResponseHasContentLength_ReportsContentLengthAsTotal()
    {
        // Arrange
        var progress = new RecordingDownloadProgress();
        var downloader = CreateDownloader(announceContentLength: true);

        // Act
        await downloader.DownloadToFileAsync(
            downloadUrl: "https://hoster.test/download",
            targetFilePath: targetFilePath,
            progress: progress,
            expectedSizeBytes: 4711,
            cancellationToken: CancellationToken.None
        );

        // Assert
        progress.TotalBytes.ShouldBe("archive-payload".Length);
        (await File.ReadAllTextAsync(targetFilePath)).ShouldBe("archive-payload");
    }

    [Test]
    public async Task DownloadToFileAsync_ResponseHasNoContentLength_ReportsExpectedSizeAsTotal()
    {
        // Arrange
        var progress = new RecordingDownloadProgress();
        var downloader = CreateDownloader(announceContentLength: false);

        // Act
        await downloader.DownloadToFileAsync(
            downloadUrl: "https://hoster.test/download",
            targetFilePath: targetFilePath,
            progress: progress,
            expectedSizeBytes: 4711,
            cancellationToken: CancellationToken.None
        );

        // Assert
        progress.TotalBytes.ShouldBe(4711);
        (await File.ReadAllTextAsync(targetFilePath)).ShouldBe("archive-payload");
    }

    [Test]
    public async Task DownloadToFileAsync_NeitherContentLengthNorExpectedSize_ReportsUnknownTotal()
    {
        // Arrange
        var progress = new RecordingDownloadProgress();
        var downloader = CreateDownloader(announceContentLength: false);

        // Act
        await downloader.DownloadToFileAsync(
            downloadUrl: "https://hoster.test/download",
            targetFilePath: targetFilePath,
            progress: progress,
            expectedSizeBytes: null,
            cancellationToken: CancellationToken.None
        );

        // Assert
        progress.TotalBytes.ShouldBeNull();
    }

    [Test]
    public async Task DownloadToFileAsync_ServerRespondsWithServerError_ThrowsWithStatusUrlAndBody()
    {
        // Arrange
        var downloader = CreateFailingDownloader(
            statusCode: HttpStatusCode.InternalServerError,
            responseContent: "<html>\n  <body>Internal   Server   Error</body>\n</html>"
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            downloader.DownloadToFileAsync(
                downloadUrl: "https://hoster.test/download/file-1?token=secret-token",
                targetFilePath: targetFilePath,
                progress: new RecordingDownloadProgress(),
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe(
            "Download request for https://hoster.test/download/file-1 failed with status code 500 (InternalServerError): <html> <body>Internal Server Error</body> </html>"
        );
        exception.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        exception.Message.ShouldNotContain("secret-token");
    }

    [Test]
    public async Task DownloadToFileAsync_ServerRespondsWithNotFound_ThrowsHosterFileNotFound()
    {
        // Arrange
        var downloader = CreateFailingDownloader(
            statusCode: HttpStatusCode.NotFound,
            responseContent: "File not found"
        );

        // Act
        var exception = await Should.ThrowAsync<HosterFileNotFoundException>(() =>
            downloader.DownloadToFileAsync(
                downloadUrl: "https://hoster.test/download/file-1?token=secret-token",
                targetFilePath: targetFilePath,
                progress: new RecordingDownloadProgress(),
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe(
            "Download request for https://hoster.test/download/file-1 failed with status code 404 (NotFound): File not found"
        );
        File.Exists(targetFilePath).ShouldBeFalse();
    }

    private static HosterFileDownloader CreateFailingDownloader(
        HttpStatusCode statusCode,
        string responseContent
    )
    {
        var httpClient = new HttpClient(new StubFailureHandler(statusCode, responseContent));

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.DownloadHttpClientName))
            .Returns(httpClient);

        return new HosterFileDownloader(new HttpClientProvider(httpClientFactoryMock.Object));
    }

    private static HosterFileDownloader CreateDownloader(bool announceContentLength)
    {
        var httpClient = new HttpClient(
            new StubDownloadHandler("archive-payload", announceContentLength)
        );

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.DownloadHttpClientName))
            .Returns(httpClient);

        return new HosterFileDownloader(new HttpClientProvider(httpClientFactoryMock.Object));
    }

    private sealed class RecordingDownloadProgress : ITransferProgress
    {
        public long? TotalBytes { get; private set; }

        public void BeginFile(long? totalBytes)
        {
            TotalBytes = totalBytes;
        }

        public void ReportBytesTransferred(long bytes) { }
    }

    private sealed class StubDownloadHandler(string responseContent, bool announceContentLength)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var payload = Encoding.UTF8.GetBytes(responseContent);

            HttpContent content = announceContentLength
                ? new ByteArrayContent(payload)
                : new StreamContent(new ForwardOnlyStream(payload));

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
            );
        }
    }

    private sealed class StubFailureHandler(HttpStatusCode statusCode, string responseContent)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(
                new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseContent, Encoding.UTF8),
                }
            );
        }
    }

    private sealed class ForwardOnlyStream(byte[] payload) : Stream
    {
        private int position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesToCopy = Math.Min(count, payload.Length - position);
            Array.Copy(payload, position, buffer, offset, bytesToCopy);
            position += bytesToCopy;

            return bytesToCopy;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
