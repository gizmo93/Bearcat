using System.Net;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.HxFile.Api;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.HxFile;

public class ApiClientTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IHxFileApi> apiMock = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IHxFileApi>(MockBehavior.Strict);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var temporaryFile in temporaryFiles.Where(File.Exists))
        {
            File.Delete(temporaryFile);
        }
    }

    [Test]
    public async Task DownloadFileAsync_ApiReturnsDirectLink_WritesFileToTargetPath()
    {
        // Arrange
        var handler = new RecordingDownloadHandler("archive-payload");
        var apiClient = CreateApiClient(handler);
        var targetFilePath = CreateTemporaryFilePath();

        apiMock
            .Setup(x => x.GetDirectLinkAsync("api-key", "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new DirectLinkResponse
                {
                    Msg = "OK",
                    Status = (int)HttpStatusCode.OK,
                    Result = new DirectLinkResult
                    {
                        Size = "2097152",
                        Url = "https://d165.hxfile.test/d/5vgk/archive.part01.rar",
                    },
                }
            );

        // Act
        await apiClient.DownloadFileAsync(
            apiKey: "api-key",
            fileCode: "abc123",
            targetFilePath: targetFilePath,
            progress: NullDownloadProgress.Instance,
            expectedSizeBytes: null,
            cancellationToken: CancellationToken.None
        );

        // Assert
        handler
            .RequestUri?.ToString()
            .ShouldBe("https://d165.hxfile.test/d/5vgk/archive.part01.rar");
        (await File.ReadAllTextAsync(targetFilePath)).ShouldBe("archive-payload");
        File.Exists(targetFilePath + ".part").ShouldBeFalse();
    }

    [Test]
    public async Task DownloadFileAsync_ApiDeniesPremiumViewPoint_ThrowsWithApiMessage()
    {
        // Arrange
        var handler = new RecordingDownloadHandler("archive-payload");
        var apiClient = CreateApiClient(handler);
        var targetFilePath = CreateTemporaryFilePath();

        apiMock
            .Setup(x => x.GetDirectLinkAsync("api-key", "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new DirectLinkResponse
                {
                    Msg = "You dont have premium view point",
                    Status = (int)HttpStatusCode.Forbidden,
                }
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.DownloadFileAsync(
                apiKey: "api-key",
                fileCode: "abc123",
                targetFilePath: targetFilePath,
                progress: NullDownloadProgress.Instance,
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("You dont have premium view point");
        handler.RequestUri.ShouldBeNull();
        File.Exists(targetFilePath).ShouldBeFalse();
        File.Exists(targetFilePath + ".part").ShouldBeFalse();
    }

    [Test]
    public async Task GetFileSizesAsync_FileInfoCarriesSizes_ReturnsSizePerFileCode()
    {
        // Arrange
        var apiClient = CreateApiClient(new RecordingDownloadHandler("unused"));

        apiMock
            .As<IXFilesharingApi>()
            .Setup(x =>
                x.GetFileInfoAsync("api-key", "abc123,def456", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new FileInfoResponse
                {
                    Msg = "OK",
                    Status = (int)HttpStatusCode.OK,
                    Results =
                    [
                        new FileInfoResult { FileCode = "abc123", Size = "106954757" },
                        new FileInfoResult { FileCode = "def456", Size = null },
                    ],
                }
            );

        // Act
        var result = await apiClient.GetFileSizesAsync(
            apiKey: "api-key",
            fileCodes: new HashSet<string> { "abc123", "def456" },
            cancellationToken: CancellationToken.None
        );

        // Assert
        result["abc123"].ShouldBe(106954757);
        result.ShouldNotContainKey("def456");
    }

    private ApiClient CreateApiClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.DownloadHttpClientName))
            .Returns(httpClient);
        var httpClientProvider = new HttpClientProvider(httpClientFactoryMock.Object);

        return new ApiClient(
            apiMock.Object,
            httpClientProvider,
            new HosterFileDownloader(httpClientProvider)
        );
    }

    private string CreateTemporaryFilePath()
    {
        var filePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.rar");
        temporaryFiles.Add(filePath);
        return filePath;
    }

    private sealed class RecordingDownloadHandler(string responseContent) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseContent),
                }
            );
        }
    }
}
