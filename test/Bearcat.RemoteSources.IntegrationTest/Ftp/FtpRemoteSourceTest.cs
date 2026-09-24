using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Abstractions.Transfers;
using Bearcat.RemoteSources.Ftp;
using FluentFTP.Exceptions;
using Shouldly;

namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

[TestFixture(FtpTestServer.VsftpdPlain, FtpTlsProvider.System)]
[TestFixture(FtpTestServer.VsftpdPlain, FtpTlsProvider.BouncyCastle)]
[TestFixture(FtpTestServer.VsftpdExplicitTls, FtpTlsProvider.System)]
[TestFixture(FtpTestServer.VsftpdExplicitTls, FtpTlsProvider.BouncyCastle)]
[TestFixture(FtpTestServer.VsftpdExplicitTlsWithoutSessionResumption, FtpTlsProvider.System)]
[TestFixture(FtpTestServer.VsftpdExplicitTlsWithoutSessionResumption, FtpTlsProvider.BouncyCastle)]
[TestFixture(FtpTestServer.VsftpdImplicitTls, FtpTlsProvider.System)]
[TestFixture(FtpTestServer.VsftpdImplicitTls, FtpTlsProvider.BouncyCastle)]
[TestFixture(FtpTestServer.Glftpd, FtpTlsProvider.System)]
[TestFixture(FtpTestServer.Glftpd, FtpTlsProvider.BouncyCastle)]
public class FtpRemoteSourceTest(FtpTestServer testServer, FtpTlsProvider tlsProvider)
{
    private readonly FtpRemoteSource remoteSource = new();
    private FtpServer server = null!;
    private string downloadDirectory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        server = await FtpTestEnvironment.GetServerAsync(testServer);
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
    public async Task ListFoldersAsync_Root_ReturnsFoldersAndSymlinkedFoldersSortedWithoutFilesAndBrokenLinks()
    {
        // Arrange
        await using var session = await OpenSessionAsync();

        // Act
        var folders = await session.ListFoldersAsync("/", CancellationToken.None);

        // Assert
        folders
            .Select(folder => folder.Name)
            .ShouldBe([
                FtpTestData.SymlinkedReleaseFolder,
                FtpTestData.ReleaseOneFolder,
                FtpTestData.ReleaseTwoFolder,
            ]);
        folders[0].FullPath.ShouldBe($"/{FtpTestData.SymlinkedReleaseFolder}");
        folders[1].FullPath.ShouldBe($"/{FtpTestData.ReleaseOneFolder}");
        folders[1].ModifiedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ListFoldersAsync_FolderWithSymlinkedSubfolder_ReturnsSymlinkedSubfolder()
    {
        // Arrange
        await using var session = await OpenSessionAsync();

        // Act
        var folders = await session.ListFoldersAsync(
            $"/{FtpTestData.ReleaseTwoFolder}",
            CancellationToken.None
        );

        // Assert
        folders.Select(folder => folder.Name).ShouldBe([FtpTestData.SymlinkedSubfolder]);
        folders[0]
            .FullPath.ShouldBe($"/{FtpTestData.ReleaseTwoFolder}/{FtpTestData.SymlinkedSubfolder}");
    }

    [Test]
    public async Task ListFoldersAsync_ReleaseFolder_ReturnsSubfoldersIncludingCompleteMarker()
    {
        // Arrange
        await using var session = await OpenSessionAsync();

        // Act
        var folders = await session.ListFoldersAsync(
            $"/{FtpTestData.ReleaseOneFolder}",
            CancellationToken.None
        );

        // Assert
        folders
            .Select(folder => folder.Name)
            .ShouldBe(["Sample", "Subs", FtpTestData.CompleteMarkerFolder]);
        folders
            .Select(folder => folder.FullPath)
            .ShouldBe([
                $"/{FtpTestData.ReleaseOneFolder}/Sample",
                $"/{FtpTestData.ReleaseOneFolder}/Subs",
                $"/{FtpTestData.ReleaseOneFolder}/{FtpTestData.CompleteMarkerFolder}",
            ]);
    }

    [Test]
    public async Task ListFilesRecursiveAsync_ReleaseFolder_ReturnsFilesAndSymlinkedFilesWithoutMarkerFolders()
    {
        // Arrange
        await using var session = await OpenSessionAsync();
        var releaseFiles = new Dictionary<string, byte[]>(
            FtpTestData.GetReleaseFiles(FtpTestData.ReleaseOneFolder)
        )
        {
            [FtpTestData.SymlinkedNfoRelativePath] = GetReleaseOneContent(
                FtpTestData.NfoFileRelativePath
            ),
        };
        var expectedFiles = CreateExpectedFiles($"/{FtpTestData.ReleaseOneFolder}", releaseFiles);

        // Act
        var files = await session.ListFilesRecursiveAsync(
            $"/{FtpTestData.ReleaseOneFolder}",
            CancellationToken.None
        );

        // Assert
        files.ShouldBe(expectedFiles);
    }

    [Test]
    public async Task ListFilesRecursiveAsync_FolderWithSymlinkedSubfolder_ListsFilesBehindSymlink()
    {
        // Arrange
        await using var session = await OpenSessionAsync();
        var expectedFiles = CreateExpectedFiles(
            $"/{FtpTestData.ReleaseTwoFolder}",
            GetReleaseTwoFilesWithSymlinkedSubfolder()
        );

        // Act
        var files = await session.ListFilesRecursiveAsync(
            $"/{FtpTestData.ReleaseTwoFolder}",
            CancellationToken.None
        );

        // Assert
        files.ShouldBe(expectedFiles);
    }

    [Test]
    public async Task ListFilesRecursiveAsync_SymlinkedFolder_ListsFilesBehindSymlinkWithLinkPaths()
    {
        // Arrange
        await using var session = await OpenSessionAsync();
        var expectedFiles = CreateExpectedFiles(
            $"/{FtpTestData.SymlinkedReleaseFolder}",
            GetReleaseTwoFilesWithSymlinkedSubfolder()
        );

        // Act
        var files = await session.ListFilesRecursiveAsync(
            $"/{FtpTestData.SymlinkedReleaseFolder}",
            CancellationToken.None
        );

        // Assert
        files.ShouldBe(expectedFiles);
    }

    [Test]
    public async Task DownloadFileAsync_AllReleaseFilesOnOneSession_DownloadsIdenticalContent()
    {
        // Arrange
        await using var session = await OpenSessionAsync();
        var files = await session.ListFilesRecursiveAsync(
            $"/{FtpTestData.ReleaseOneFolder}",
            CancellationToken.None
        );

        // Act
        foreach (var file in files)
        {
            await session.DownloadFileAsync(
                file,
                GetLocalFilePath(file),
                NullTransferProgress.Instance,
                CancellationToken.None
            );
        }

        // Assert
        foreach (
            var (relativePath, content) in FtpTestData.GetReleaseFiles(FtpTestData.ReleaseOneFolder)
        )
        {
            var localFilePath = Path.Combine(downloadDirectory, relativePath);
            (await File.ReadAllBytesAsync(localFilePath)).ShouldBe(content, relativePath);
        }

        (
            await File.ReadAllBytesAsync(
                Path.Combine(downloadDirectory, FtpTestData.SymlinkedNfoRelativePath)
            )
        ).ShouldBe(GetReleaseOneContent(FtpTestData.NfoFileRelativePath));

        Directory
            .GetFiles(downloadDirectory, "*.part", SearchOption.AllDirectories)
            .ShouldBeEmpty();
    }

    [Test]
    public async Task DownloadFileAsync_TwoConsecutiveDownloadsOnSameSession_BothSucceed()
    {
        // Arrange
        await using var session = await OpenSessionAsync();
        var largeFile = CreateReleaseOneFileDto(FtpTestData.LargeFileRelativePath);
        var nfoFile = CreateReleaseOneFileDto(FtpTestData.NfoFileRelativePath);

        // Act
        await session.DownloadFileAsync(
            largeFile,
            GetLocalFilePath(largeFile),
            NullTransferProgress.Instance,
            CancellationToken.None
        );
        await session.DownloadFileAsync(
            nfoFile,
            GetLocalFilePath(nfoFile),
            NullTransferProgress.Instance,
            CancellationToken.None
        );

        // Assert
        (await File.ReadAllBytesAsync(GetLocalFilePath(largeFile))).ShouldBe(
            GetReleaseOneContent(FtpTestData.LargeFileRelativePath)
        );
        (await File.ReadAllBytesAsync(GetLocalFilePath(nfoFile))).ShouldBe(
            GetReleaseOneContent(FtpTestData.NfoFileRelativePath)
        );
    }

    [Test]
    public async Task DownloadFileAsync_LargeFile_ReportsProgressDeltasSummingToFileSize()
    {
        // Arrange
        await using var session = await OpenSessionAsync();
        var file = CreateReleaseOneFileDto(FtpTestData.LargeFileRelativePath);
        var progress = new RecordingTransferProgress();

        // Act
        await session.DownloadFileAsync(
            file,
            GetLocalFilePath(file),
            progress,
            CancellationToken.None
        );

        // Assert
        progress.BeganFiles.ShouldBe([file.SizeBytes]);
        progress.TransferredBytes.Count.ShouldBeGreaterThan(1);
        progress.TransferredBytes.ShouldAllBe(bytes => bytes > 0);
        progress.TransferredBytes.Sum().ShouldBe(file.SizeBytes);
    }

    [Test]
    public async Task DownloadFileAsync_CancelledDuringTransfer_ThrowsAndDeletesPartialFile()
    {
        // Arrange
        await using var session = await OpenSessionAsync();
        var file = CreateReleaseOneFileDto(FtpTestData.LargeFileRelativePath);
        var localFilePath = GetLocalFilePath(file);
        using var cancellationTokenSource = new CancellationTokenSource();
        var progress = new RecordingTransferProgress(cancellationTokenSource.Cancel);

        // Act
        var exception = await Should.ThrowAsync<Exception>(() =>
            session.DownloadFileAsync(file, localFilePath, progress, cancellationTokenSource.Token)
        );

        // Assert
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        progress.TransferredBytes.ShouldNotBeEmpty();
        File.Exists(localFilePath + ".part").ShouldBeFalse();
        File.Exists(localFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task DownloadFileAsync_RemoteFileMissing_ThrowsAndDeletesPartialFile()
    {
        // Arrange
        await using var session = await OpenSessionAsync();
        var file = new RemoteFileDto(
            RelativePath: "missing.rar",
            FullPath: $"/{FtpTestData.ReleaseOneFolder}/missing.rar",
            SizeBytes: 100
        );
        var localFilePath = GetLocalFilePath(file);

        // Act
        var exception = await Should.ThrowAsync<Exception>(() =>
            session.DownloadFileAsync(
                file,
                localFilePath,
                NullTransferProgress.Instance,
                CancellationToken.None
            )
        );

        // Assert
        exception.ShouldBeAssignableTo<FtpException>();
        File.Exists(localFilePath + ".part").ShouldBeFalse();
        File.Exists(localFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task OpenSessionAsync_WrongPassword_ThrowsAuthenticationException()
    {
        // Arrange
        var config = server.CreateConfig(tlsProvider, password: "wrong-password");

        // Act
        var action = () => remoteSource.OpenSessionAsync(config, CancellationToken.None);

        // Assert
        await action.ShouldThrowAsync<FtpAuthenticationException>();
    }

    [Test]
    public async Task DownloadFileAsync_TwoSessionsInParallel_BothDownloadIdenticalContent()
    {
        // Arrange
        await using var firstSession = await OpenSessionAsync();
        await using var secondSession = await OpenSessionAsync();
        var largeFile = CreateReleaseOneFileDto(FtpTestData.LargeFileRelativePath);
        var secondLargeFile = CreateReleaseOneFileDto("release.one-group.r00");

        // Act
        await Task.WhenAll(
            firstSession.DownloadFileAsync(
                largeFile,
                GetLocalFilePath(largeFile),
                NullTransferProgress.Instance,
                CancellationToken.None
            ),
            secondSession.DownloadFileAsync(
                secondLargeFile,
                GetLocalFilePath(secondLargeFile),
                NullTransferProgress.Instance,
                CancellationToken.None
            )
        );

        // Assert
        (await File.ReadAllBytesAsync(GetLocalFilePath(largeFile))).ShouldBe(
            GetReleaseOneContent(largeFile.RelativePath)
        );
        (await File.ReadAllBytesAsync(GetLocalFilePath(secondLargeFile))).ShouldBe(
            GetReleaseOneContent(secondLargeFile.RelativePath)
        );
    }

    private Task<IRemoteSourceSession> OpenSessionAsync()
    {
        return remoteSource.OpenSessionAsync(
            server.CreateConfig(tlsProvider),
            CancellationToken.None
        );
    }

    private string GetLocalFilePath(RemoteFileDto file)
    {
        return Path.Combine(downloadDirectory, file.RelativePath);
    }

    private static List<RemoteFileDto> CreateExpectedFiles(
        string folderPath,
        IReadOnlyDictionary<string, byte[]> files
    )
    {
        return files
            .OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => new RemoteFileDto(
                RelativePath: file.Key,
                FullPath: $"{folderPath}/{file.Key}",
                SizeBytes: file.Value.Length
            ))
            .ToList();
    }

    private static Dictionary<string, byte[]> GetReleaseTwoFilesWithSymlinkedSubfolder()
    {
        var files = new Dictionary<string, byte[]>(
            FtpTestData.GetReleaseFiles(FtpTestData.ReleaseTwoFolder)
        );
        var linkedFiles = FtpTestData.GetReleaseFiles(
            $"{FtpTestData.ReleaseOneFolder}/{FtpTestData.SymlinkedSubfolderTarget}"
        );

        foreach (var (relativePath, content) in linkedFiles)
        {
            files[$"{FtpTestData.SymlinkedSubfolder}/{relativePath}"] = content;
        }

        return files;
    }

    private static RemoteFileDto CreateReleaseOneFileDto(string relativePath)
    {
        return new RemoteFileDto(
            RelativePath: relativePath,
            FullPath: $"/{FtpTestData.ReleaseOneFolder}/{relativePath}",
            SizeBytes: GetReleaseOneContent(relativePath).Length
        );
    }

    private static byte[] GetReleaseOneContent(string relativePath)
    {
        return FtpTestData.Files[$"{FtpTestData.ReleaseOneFolder}/{relativePath}"];
    }
}
