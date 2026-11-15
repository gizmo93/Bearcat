using System.Net;
using Bearcat.Media.Ffmpeg;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Media.UnitTest.Ffmpeg;

public class FfmpegInstallerTest
{
    private string dataDirectory = null!;
    private TestHttpMessageHandler httpMessageHandler = null!;
    private FfmpegInstaller installer = null!;

    [SetUp]
    public void SetUp()
    {
        dataDirectory = Path.Combine(Path.GetTempPath(), $"bearcat-ffmpeg-test-{Guid.NewGuid():N}");
        httpMessageHandler = new TestHttpMessageHandler();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(FfmpegInstaller.HttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Bearcat:DataDirectory"] = dataDirectory }
            )
            .Build();

        installer = new FfmpegInstaller(
            httpClientFactory: httpClientFactoryMock.Object,
            configuration: configuration,
            logger: NullLogger<FfmpegInstaller>.Instance
        );
    }

    [TearDown]
    public void TearDown()
    {
        installer.Dispose();
        httpMessageHandler.Dispose();

        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Test]
    public async Task InstallLatestAsync_DownloadFails_KeepsPreviousInstallAndRecordsError()
    {
        // Arrange
        var downloadSource = FfmpegDownloadSourceCatalog.FindForCurrentPlatform();
        Assume.That(downloadSource, Is.Not.Null);

        var toolsDirectory = Path.Combine(dataDirectory, "tools");
        var installedExecutablePath = Path.Combine(
            toolsDirectory,
            "ffmpeg",
            downloadSource!.ExecutableFileName
        );

        Directory.CreateDirectory(Path.GetDirectoryName(installedExecutablePath)!);
        await File.WriteAllTextAsync(installedExecutablePath, "previous ffmpeg");

        httpMessageHandler.Enqueue(request => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            RequestMessage = request,
        });

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            installer.InstallLatestAsync(CancellationToken.None)
        );

        // Assert
        (await File.ReadAllTextAsync(installedExecutablePath)).ShouldBe("previous ffmpeg");
        Directory.GetDirectories(toolsDirectory).ShouldBe([Path.Combine(toolsDirectory, "ffmpeg")]);
        installer.GetInstallState().LastErrorMessage.ShouldBe(exception.Message);
        installer.GetInstallState().IsRunning.ShouldBeFalse();
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = [];

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response)
        {
            responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            responses.Count.ShouldBeGreaterThan(0);
            return Task.FromResult(responses.Dequeue()(request));
        }
    }
}
