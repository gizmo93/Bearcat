using Bearcat.Abstractions.Transfers;
using Bearcat.RemoteSources.Ftp;
using Shouldly;

namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

[TestFixture(
    FtpTlsProvider.System,
    Ignore = "System TLS (SslStream) does not resume the control TLS session on data connections: FtpCommandException 522 'SSL connection failed: session reuse required' (macOS arm64, .NET 10)"
)]
[TestFixture(FtpTlsProvider.BouncyCastle)]
public class FtpRemoteSourceSslSessionReuseTest(FtpTlsProvider tlsProvider)
{
    private readonly FtpRemoteSource remoteSource = new();
    private FtpServer server = null!;
    private string downloadDirectory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        server = await FtpTestEnvironment.GetServerAsync(
            FtpTestServer.VsftpdExplicitTlsWithSessionReuse
        );
    }

    [SetUp]
    public void SetUp()
    {
        downloadDirectory = Path.Combine(
            Path.GetTempPath(),
            "bearcat-remote-sources-tests",
            Guid.NewGuid().ToString("N")
        );
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(downloadDirectory))
        {
            Directory.Delete(downloadDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ListFilesRecursiveAsync_ServerRequiresSslSessionReuse_ListsFiles()
    {
        // Arrange
        await using var session = await remoteSource.OpenSessionAsync(
            server.CreateConfig(tlsProvider),
            CancellationToken.None
        );

        // Act
        var files = await session.ListFilesRecursiveAsync(
            $"/{FtpTestData.ReleaseTwoFolder}",
            CancellationToken.None
        );

        // Assert
        files
            .Select(file => file.RelativePath)
            .ShouldBe([
                "Linked.Subs/Nested/empty.srt",
                "release.two-group.nfo",
                "release.two-group.rar",
            ]);
    }

    [Test]
    public async Task DownloadFileAsync_ServerRequiresSslSessionReuse_DownloadsTwoFilesOnSameSession()
    {
        // Arrange
        await using var session = await remoteSource.OpenSessionAsync(
            server.CreateConfig(tlsProvider),
            CancellationToken.None
        );
        var files = await session.ListFilesRecursiveAsync(
            $"/{FtpTestData.ReleaseTwoFolder}",
            CancellationToken.None
        );

        // Act
        foreach (var file in files)
        {
            await session.DownloadFileAsync(
                file,
                Path.Combine(downloadDirectory, file.RelativePath),
                NullTransferProgress.Instance,
                CancellationToken.None
            );
        }

        // Assert
        foreach (
            var (relativePath, content) in FtpTestData.GetReleaseFiles(FtpTestData.ReleaseTwoFolder)
        )
        {
            (await File.ReadAllBytesAsync(Path.Combine(downloadDirectory, relativePath))).ShouldBe(
                content
            );
        }
    }
}
