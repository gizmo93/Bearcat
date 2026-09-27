using System.IO.Hashing;
using System.Text;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UnitTest.UseCases.PostToForums;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.Extraction;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.SfvVerification;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;

public class RemoteDownloadVerificationAndExtractionServiceTest
{
    private const string TemporaryExtractionFolderName = ".bearcat-extraction";

    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Unspecified);

    private FakeRemoteDownloadVerificationAndExtractionRepository repository = null!;
    private FakeArchiveExtractor archiveExtractor = null!;
    private FakeNotificationService notificationService = null!;
    private string localFolderPath = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeRemoteDownloadVerificationAndExtractionRepository();
        archiveExtractor = new FakeArchiveExtractor();
        notificationService = new FakeNotificationService();
        localFolderPath = Directory
            .CreateDirectory(
                Path.Combine(
                    Path.GetTempPath(),
                    $"bearcat-extraction-{Guid.NewGuid():N}",
                    "Show.S01E01.German.1080p.WEB.x264-GRP"
                )
            )
            .FullName;
    }

    [TearDown]
    public void TearDown()
    {
        var rootFolderPath = Path.GetDirectoryName(localFolderPath)!;

        if (Directory.Exists(rootFolderPath))
        {
            Directory.Delete(rootFolderPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_NoSfvAndExtractionDisabled_MarksReadyForReleaseCreation()
    {
        // Arrange
        WriteFile("release.rar", "volume");
        var download = AddDownload(extractArchives: false);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.ReadyForReleaseCreation);
        download.ArchivesExtractedAt.ShouldBeNull();
        repository.SavedStates.ShouldBe([
            RemoteSourceDownloadState.Downloaded,
            RemoteSourceDownloadState.ReadyForReleaseCreation,
        ]);
        archiveExtractor.DestinationFolderPaths.ShouldBeEmpty();
        File.Exists(Path.Combine(localFolderPath, "release.rar")).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_SfvMatches_VerifiesAndMarksReadyForReleaseCreation()
    {
        // Arrange
        WriteFile("release.rar", "volume");
        WriteFile("release.sfv", CreateSfvLine("release.rar", "volume"));
        var download = AddDownload(extractArchives: false);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.ReadyForReleaseCreation);
        download.ErrorMessage.ShouldBeNull();
        repository.SavedStates.ShouldBe([
            RemoteSourceDownloadState.Downloaded,
            RemoteSourceDownloadState.Verifying,
            RemoteSourceDownloadState.ReadyForReleaseCreation,
        ]);
        notificationService.Created.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_SfvMismatch_FailsKeepsFilesAndNotifies()
    {
        // Arrange
        WriteFile("release.rar", "corrupted");
        WriteFile("release.sfv", CreateSfvLine("release.rar", "volume"));
        var download = AddDownload(extractArchives: true);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Failed);
        download.ErrorMessage!.ShouldContain("wrong CRC32 checksum: release.rar");
        download.ErrorMessage!.ShouldContain($"files were kept in {localFolderPath}");
        archiveExtractor.DestinationFolderPaths.ShouldBeEmpty();
        File.Exists(Path.Combine(localFolderPath, "release.rar")).ShouldBeTrue();
        var notification = notificationService.Created.ShouldHaveSingleItem();
        notification.Kind.ShouldBe(NotificationKind.RemoteDownloadFailed);
        notification.Message.ShouldContain(download.FolderName);
    }

    [Test]
    public async Task ProcessAsync_ExtractionEnabled_MovesExtractedFilesAndDeletesVolumesAndCoveredSfv()
    {
        // Arrange
        var firstVolumeFilePath = WriteFile("release.rar", "first volume");
        var secondVolumeFilePath = WriteFile("release.r00", "second volume");
        WriteFile(
            "release.sfv",
            CreateSfvLine("release.rar", "first volume"),
            CreateSfvLine("release.r00", "second volume")
        );
        WriteFile("release.nfo", "nfo");
        var subsVolumeFilePath = WriteFile(Path.Combine("Subs", "subs.rar"), "subtitles");
        WriteFile(Path.Combine("Subs", "english.idx"), "index");
        WriteFile(
            Path.Combine("Subs", "subs.sfv"),
            CreateSfvLine("subs.rar", "subtitles"),
            CreateSfvLine("english.idx", "index")
        );
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstVolumeFilePath, [firstVolumeFilePath, secondVolumeFilePath]),
            new Dictionary<string, string>
            {
                ["movie.mkv"] = "movie",
                [Path.Combine("Sample", "sample.mkv")] = "sample",
            }
        );
        archiveExtractor.AddArchive(
            new ArchiveToExtract(subsVolumeFilePath, [subsVolumeFilePath]),
            new Dictionary<string, string> { ["english.sub"] = "subtitles" }
        );
        var download = AddDownload(extractArchives: true);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.ReadyForReleaseCreation);
        download.ArchivesExtractedAt.ShouldBe(Now);
        repository.SavedStates.ShouldBe([
            RemoteSourceDownloadState.Downloaded,
            RemoteSourceDownloadState.Verifying,
            RemoteSourceDownloadState.Extracting,
            RemoteSourceDownloadState.ReadyForReleaseCreation,
        ]);
        archiveExtractor.DestinationFolderPaths.ShouldBe([
            Path.Join(localFolderPath, TemporaryExtractionFolderName, "1"),
            Path.Join(localFolderPath, "Subs", TemporaryExtractionFolderName, "2"),
        ]);
        GetRelativeFilePaths()
            .ShouldBe(
                [
                    "movie.mkv",
                    "release.nfo",
                    Path.Combine("Sample", "sample.mkv"),
                    Path.Combine("Subs", "english.idx"),
                    Path.Combine("Subs", "english.sub"),
                    Path.Combine("Subs", "subs.sfv"),
                ],
                ignoreOrder: true
            );
        Directory
            .GetDirectories(
                localFolderPath,
                TemporaryExtractionFolderName,
                SearchOption.AllDirectories
            )
            .ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_ExtractionEnabledWithoutArchives_MarksReadyWithoutExtractionTime()
    {
        // Arrange
        WriteFile("movie.mkv", "movie");
        var download = AddDownload(extractArchives: true);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.ReadyForReleaseCreation);
        download.ArchivesExtractedAt.ShouldBeNull();
        repository.SavedStates.ShouldBe([
            RemoteSourceDownloadState.Downloaded,
            RemoteSourceDownloadState.Extracting,
            RemoteSourceDownloadState.ReadyForReleaseCreation,
        ]);
    }

    [Test]
    public async Task ProcessAsync_ExtractedEntryAlreadyExists_FailsAndRemovesTemporaryFolder()
    {
        // Arrange
        var firstVolumeFilePath = WriteFile("release.rar", "volume");
        WriteFile("movie.mkv", "existing movie");
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstVolumeFilePath, [firstVolumeFilePath]),
            new Dictionary<string, string> { ["movie.mkv"] = "extracted movie" }
        );
        var download = AddDownload(extractArchives: true);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Failed);
        download.ErrorMessage!.ShouldContain("movie.mkv");
        download.ErrorMessage!.ShouldContain("already exist");
        download.ArchivesExtractedAt.ShouldBeNull();
        (await File.ReadAllTextAsync(Path.Combine(localFolderPath, "movie.mkv"))).ShouldBe(
            "existing movie"
        );
        File.Exists(firstVolumeFilePath).ShouldBeTrue();
        Directory.Exists(Path.Join(localFolderPath, TemporaryExtractionFolderName)).ShouldBeFalse();
        notificationService
            .Created.ShouldHaveSingleItem()
            .Kind.ShouldBe(NotificationKind.RemoteDownloadFailed);
    }

    [Test]
    public async Task ProcessAsync_TwoArchivesInSameFolderContainSameEntry_FailsWithoutMovingAnything()
    {
        // Arrange
        var firstArchiveFilePath = WriteFile("first.rar", "first volume");
        var secondArchiveFilePath = WriteFile("second.rar", "second volume");
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstArchiveFilePath, [firstArchiveFilePath]),
            new Dictionary<string, string>
            {
                ["movie.mkv"] = "first movie",
                ["first.nfo"] = "first nfo",
            }
        );
        archiveExtractor.AddArchive(
            new ArchiveToExtract(secondArchiveFilePath, [secondArchiveFilePath]),
            new Dictionary<string, string> { ["movie.mkv"] = "second movie" }
        );
        var download = AddDownload(extractArchives: true);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Failed);
        download.ErrorMessage!.ShouldContain("movie.mkv");
        download.ErrorMessage!.ShouldNotContain("first.nfo");
        download.ErrorMessage!.ShouldContain("more than one archive");
        archiveExtractor.DestinationFolderPaths.ShouldBe([
            Path.Join(localFolderPath, TemporaryExtractionFolderName, "1"),
            Path.Join(localFolderPath, TemporaryExtractionFolderName, "2"),
        ]);
        GetRelativeFilePaths().ShouldBe(["first.rar", "second.rar"], ignoreOrder: true);
        Directory.Exists(Path.Join(localFolderPath, TemporaryExtractionFolderName)).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_SecondArchiveConflictsWithExistingFile_DoesNotMoveEntriesOfFirstArchive()
    {
        // Arrange
        var firstArchiveFilePath = WriteFile("release.rar", "release volume");
        var subsArchiveFilePath = WriteFile(Path.Combine("Subs", "subs.rar"), "subs volume");
        WriteFile(Path.Combine("Subs", "english.sub"), "existing subtitles");
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstArchiveFilePath, [firstArchiveFilePath]),
            new Dictionary<string, string> { ["movie.mkv"] = "movie" }
        );
        archiveExtractor.AddArchive(
            new ArchiveToExtract(subsArchiveFilePath, [subsArchiveFilePath]),
            new Dictionary<string, string> { ["english.sub"] = "extracted subtitles" }
        );
        var download = AddDownload(extractArchives: true);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Failed);
        download.ErrorMessage!.ShouldContain(Path.Combine("Subs", "english.sub"));
        File.Exists(Path.Combine(localFolderPath, "movie.mkv")).ShouldBeFalse();
        File.Exists(firstArchiveFilePath).ShouldBeTrue();
        File.Exists(subsArchiveFilePath).ShouldBeTrue();
        (
            await File.ReadAllTextAsync(Path.Combine(localFolderPath, "Subs", "english.sub"))
        ).ShouldBe("existing subtitles");
        Directory
            .GetDirectories(
                localFolderPath,
                TemporaryExtractionFolderName,
                SearchOption.AllDirectories
            )
            .ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_ExtractorReportsFailure_FailsWithItsErrorMessages()
    {
        // Arrange
        var firstVolumeFilePath = WriteFile("release.rar", "volume");
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstVolumeFilePath, [firstVolumeFilePath]),
            new Dictionary<string, string>()
        );
        archiveExtractor.FailureResult = new ArchiveExtractionResult(
            false,
            ["CRC failed in movie.mkv", "Unexpected end of archive"]
        );
        var download = AddDownload(extractArchives: true);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Failed);
        download.ErrorMessage!.ShouldContain(
            "Extracting 'release.rar' failed: CRC failed in movie.mkv | Unexpected end of archive"
        );
        File.Exists(firstVolumeFilePath).ShouldBeTrue();
        Directory.Exists(Path.Join(localFolderPath, TemporaryExtractionFolderName)).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_ArchiveSearchThrows_Fails()
    {
        // Arrange
        WriteFile("release.r00", "volume");
        archiveExtractor.ExceptionWhenSearching = new InvalidOperationException(
            "The folder contains continuation volumes without a first volume"
        );
        var download = AddDownload(extractArchives: true);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Failed);
        download.ErrorMessage!.ShouldContain("continuation volumes without a first volume");
    }

    [TestCase(RemoteSourceDownloadState.Verifying)]
    [TestCase(RemoteSourceDownloadState.Extracting)]
    public async Task ProcessAsync_DownloadInterruptedByCrash_DeletesTemporaryFoldersAndProcessesAgain(
        RemoteSourceDownloadState interruptedState
    )
    {
        // Arrange
        WriteFile(Path.Combine(TemporaryExtractionFolderName, "movie.mkv"), "partial");
        WriteFile(Path.Combine("Subs", TemporaryExtractionFolderName, "english.sub"), "partial");
        WriteFile("movie.nfo", "nfo");
        var download = AddDownload(extractArchives: true, state: interruptedState);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.ReadyForReleaseCreation);
        repository.SavedStates[0].ShouldBe(RemoteSourceDownloadState.Downloaded);
        Directory
            .GetDirectories(
                localFolderPath,
                TemporaryExtractionFolderName,
                SearchOption.AllDirectories
            )
            .ShouldBeEmpty();
        File.Exists(Path.Combine(localFolderPath, "movie.nfo")).ShouldBeTrue();
    }

    [TestCase(RemoteSourceDownloadState.Failed)]
    [TestCase(RemoteSourceDownloadState.ReadyForReleaseCreation)]
    [TestCase(RemoteSourceDownloadState.Downloading)]
    public async Task ProcessAsync_DownloadInOtherState_IsLeftUntouched(
        RemoteSourceDownloadState state
    )
    {
        // Arrange
        WriteFile("release.sfv", "invalid line");
        var download = AddDownload(extractArchives: true, state: state);

        // Act
        await CreateService().ProcessAsync(CancellationToken.None);

        // Assert
        download.State.ShouldBe(state);
        repository.SavedStates.ShouldBe([state]);
        notificationService.Created.ShouldBeEmpty();
    }

    private RemoteDownloadVerificationAndExtractionService CreateService()
    {
        var archiverFactory = new Mock<IArchiverFactory>(MockBehavior.Strict);
        archiverFactory
            .Setup(factory => factory.GetArchiveExtractors())
            .Returns([archiveExtractor]);

        var fileSystemService = new Mock<IFileSystemService>(MockBehavior.Strict);
        fileSystemService
            .Setup(service =>
                service.DeleteDirectoriesByNameRecursively(It.IsAny<string>(), It.IsAny<string>())
            )
            .Returns(
                (string rootPath, string directoryName) =>
                    DeleteDirectories(rootPath, directoryName)
            );

        var timeProvider = new Mock<TimeProvider>(new ConfigurationBuilder().Build());
        timeProvider.Setup(provider => provider.GetLocalNow()).Returns(Now);

        return new RemoteDownloadVerificationAndExtractionService(
            repository,
            new SfvChecksumVerifier(NullLogger<SfvChecksumVerifier>.Instance),
            new DownloadFolderArchiveExtractionService(
                archiverFactory.Object,
                fileSystemService.Object,
                NullLogger<DownloadFolderArchiveExtractionService>.Instance
            ),
            notificationService,
            timeProvider.Object,
            NullLogger<RemoteDownloadVerificationAndExtractionService>.Instance
        );
    }

    private static List<string> DeleteDirectories(string rootPath, string directoryName)
    {
        var directoryPaths = Directory
            .GetDirectories(rootPath, directoryName, SearchOption.AllDirectories)
            .ToList();

        foreach (var directoryPath in directoryPaths)
        {
            Directory.Delete(directoryPath, recursive: true);
        }

        return directoryPaths;
    }

    private RemoteSourceDownload AddDownload(
        bool extractArchives,
        RemoteSourceDownloadState state = RemoteSourceDownloadState.Downloaded
    )
    {
        var download = new RemoteSourceDownload
        {
            Id = repository.Downloads.Count + 1,
            SourceName = "Main FTP",
            RemoteFolderPath = $"/incoming/{Path.GetFileName(localFolderPath)}",
            FolderName = Path.GetFileName(localFolderPath),
            LocalFolderPath = localFolderPath,
            ExtractArchivesBeforeReleaseCreation = extractArchives,
            State = state,
            CompletedAt = Now,
        };
        repository.Downloads.Add(download);

        return download;
    }

    private static string CreateSfvLine(string relativeFilePath, string content)
    {
        return $"{relativeFilePath} {Crc32.HashToUInt32(Encoding.UTF8.GetBytes(content)):X8}";
    }

    private string WriteFile(string relativePath, params string[] lines)
    {
        var filePath = Path.Combine(localFolderPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, string.Join('\n', lines));

        return filePath;
    }

    private List<string> GetRelativeFilePaths()
    {
        return Directory
            .GetFiles(localFolderPath, "*", SearchOption.AllDirectories)
            .Select(filePath => Path.GetRelativePath(localFolderPath, filePath))
            .ToList();
    }
}
