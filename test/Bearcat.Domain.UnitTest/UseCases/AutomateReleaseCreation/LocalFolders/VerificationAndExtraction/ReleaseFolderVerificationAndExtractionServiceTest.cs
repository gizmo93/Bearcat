using System.IO.Hashing;
using System.Text;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UnitTest.Shared;
using Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.ArchiveExtraction.Extraction;
using Bearcat.Domain.UnitTest.UseCases.PostToForums;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.Extraction;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.SfvVerification;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders.VerificationAndExtraction;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.LocalFolders.VerificationAndExtraction;

public class ReleaseFolderVerificationAndExtractionServiceTest
{
    private const string TemporaryExtractionFolderName = ".bearcat-extraction";
    private const string FolderName = "Show.S01E01.German.1080p.WEB.x264-GRP";
    private const int ObservationId = 7;

    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Unspecified);

    private static readonly DateTime LastChangedAt = new(
        2026,
        10,
        7,
        11,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private FakeArchiveExtractor archiveExtractor = null!;
    private FakeNotificationService notificationService = null!;
    private RecordingTransferProgressTracker progressTracker = null!;
    private string folderPath = null!;

    [SetUp]
    public void SetUp()
    {
        archiveExtractor = new FakeArchiveExtractor();
        notificationService = new FakeNotificationService();
        progressTracker = new RecordingTransferProgressTracker();
        folderPath = Directory
            .CreateDirectory(
                Path.Combine(
                    Path.GetTempPath(),
                    $"bearcat-extraction-{Guid.NewGuid():N}",
                    FolderName
                )
            )
            .FullName;
    }

    [TearDown]
    public void TearDown()
    {
        var rootFolderPath = Path.GetDirectoryName(folderPath)!;

        if (Directory.Exists(rootFolderPath))
        {
            Directory.Delete(rootFolderPath, recursive: true);
        }
    }

    [Test]
    public async Task TryVerifyAndExtractAsync_SfvMatches_ExtractsArchivesAndDeletesVolumesAndSfv()
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
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstVolumeFilePath, [firstVolumeFilePath, secondVolumeFilePath]),
            new Dictionary<string, string> { ["movie.mkv"] = "movie" }
        );
        var observation = CreateObservation();

        // Act
        var result = await CreateService()
            .TryVerifyAndExtractAsync(observation, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        observation.ExtractionErrorMessage.ShouldBeNull();
        observation.ExtractionFailedAt.ShouldBeNull();
        observation.FileCount.ShouldBe(1);
        observation.TotalBytes.ShouldBe(1);
        GetRelativeFilePaths().ShouldBe(["movie.mkv", "release.nfo"], ignoreOrder: true);
        notificationService.Created.ShouldBeEmpty();
    }

    [Test]
    public async Task TryVerifyAndExtractAsync_SfvAndArchive_TracksProgressWithObservationIdAndFolderName()
    {
        // Arrange
        var firstVolumeFilePath = WriteFile("release.rar", "volume");
        WriteFile("release.sfv", CreateSfvLine("release.rar", "volume"));
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstVolumeFilePath, [firstVolumeFilePath]),
            new Dictionary<string, string> { ["movie.mkv"] = "movie" }
        );
        var verificationIdentifier = new TransferIdentifier(
            TransferType.ReleaseFolderVerification,
            ObservationId
        );
        var extractionIdentifier = new TransferIdentifier(
            TransferType.ReleaseFolderExtraction,
            ObservationId
        );

        // Act
        await CreateService().TryVerifyAndExtractAsync(CreateObservation(), CancellationToken.None);

        // Assert
        progressTracker
            .PlannedFilesPerIdentifier[verificationIdentifier]
            .ShouldAllBe(file => file.SourceName == FolderName);
        progressTracker
            .PlannedFilesPerIdentifier[extractionIdentifier]
            .ShouldBe([new TransferFile(1, "release.rar", FolderName, 6, false)]);
        progressTracker.StoppedIdentifiers.ShouldBe([verificationIdentifier, extractionIdentifier]);
    }

    [Test]
    public async Task TryVerifyAndExtractAsync_NoSfvAndNoArchives_SucceedsWithoutTrackingProgress()
    {
        // Arrange
        WriteFile("movie.mkv", "movie");
        var observation = CreateObservation();

        // Act
        var result = await CreateService()
            .TryVerifyAndExtractAsync(observation, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        observation.ExtractionErrorMessage.ShouldBeNull();
        progressTracker.PlannedFilesPerIdentifier.ShouldBeEmpty();
        GetRelativeFilePaths().ShouldBe(["movie.mkv"]);
    }

    [Test]
    public async Task TryVerifyAndExtractAsync_TemporaryFolderLeftByCrash_DeletesItBeforeExtracting()
    {
        // Arrange
        WriteFile(Path.Combine(TemporaryExtractionFolderName, "movie.mkv"), "partial");
        var firstVolumeFilePath = WriteFile("release.rar", "volume");
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstVolumeFilePath, [firstVolumeFilePath]),
            new Dictionary<string, string> { ["movie.mkv"] = "movie" }
        );

        // Act
        var result = await CreateService()
            .TryVerifyAndExtractAsync(CreateObservation(), CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        GetRelativeFilePaths().ShouldBe(["movie.mkv"]);
        (await File.ReadAllTextAsync(Path.Combine(folderPath, "movie.mkv"))).ShouldBe("movie");
    }

    [Test]
    public async Task TryVerifyAndExtractAsync_SfvMismatch_StoresErrorRemeasuresFolderAndNotifies()
    {
        // Arrange
        var firstVolumeFilePath = WriteFile("release.rar", "corrupted");
        WriteFile("release.sfv", CreateSfvLine("release.rar", "volume"));
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstVolumeFilePath, [firstVolumeFilePath]),
            new Dictionary<string, string> { ["movie.mkv"] = "movie" }
        );
        var observation = CreateObservation();

        // Act
        var result = await CreateService()
            .TryVerifyAndExtractAsync(observation, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
        observation.ExtractionErrorMessage!.ShouldContain("wrong CRC32 checksum: release.rar");
        observation.ExtractionErrorMessage!.ShouldContain($"files were kept in {folderPath}");
        observation.ExtractionFailedAt.ShouldBe(Now);
        observation.FileCount.ShouldBe(2);
        observation.TotalBytes.ShouldBe(GetTotalBytes());
        observation.LastChangedAt.ShouldBe(LastChangedAt);
        archiveExtractor.DestinationFolderPaths.ShouldBeEmpty();
        File.Exists(firstVolumeFilePath).ShouldBeTrue();
        var notification = notificationService.Created.ShouldHaveSingleItem();
        notification.Kind.ShouldBe(NotificationKind.ReleaseFolderExtractionFailed);
        notification.Message.ShouldContain(FolderName);
        notification.Message.ShouldContain($"files were kept in {folderPath}");
    }

    [Test]
    public async Task TryVerifyAndExtractAsync_ExtractorReportsFailure_KeepsVolumesAndRemovesTemporaryFolder()
    {
        // Arrange
        var firstVolumeFilePath = WriteFile("release.rar", "volume");
        archiveExtractor.AddArchive(
            new ArchiveToExtract(firstVolumeFilePath, [firstVolumeFilePath]),
            new Dictionary<string, string> { ["movie.mkv"] = "movie" }
        );
        archiveExtractor.FailureResult = new ArchiveExtractionResult(false, ["CRC failed"]);
        var observation = CreateObservation();

        // Act
        var result = await CreateService()
            .TryVerifyAndExtractAsync(observation, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
        observation.ExtractionErrorMessage!.ShouldContain(
            "Extracting 'release.rar' failed: CRC failed"
        );
        GetRelativeFilePaths().ShouldBe(["release.rar"]);
        Directory.Exists(Path.Join(folderPath, TemporaryExtractionFolderName)).ShouldBeFalse();
        progressTracker.StoppedIdentifiers.ShouldBe([
            new TransferIdentifier(TransferType.ReleaseFolderExtraction, ObservationId),
        ]);
        notificationService
            .Created.ShouldHaveSingleItem()
            .Kind.ShouldBe(NotificationKind.ReleaseFolderExtractionFailed);
    }

    [Test]
    public async Task TryVerifyAndExtractAsync_Canceled_ThrowsWithoutStoringError()
    {
        // Arrange
        WriteFile("release.rar", "volume");
        WriteFile("release.sfv", CreateSfvLine("release.rar", "volume"));
        var observation = CreateObservation();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var action = () =>
            CreateService().TryVerifyAndExtractAsync(observation, cancellationTokenSource.Token);

        // Assert
        await action.ShouldThrowAsync<OperationCanceledException>();
        observation.ExtractionErrorMessage.ShouldBeNull();
        notificationService.Created.ShouldBeEmpty();
    }

    private ReleaseFolderVerificationAndExtractionService CreateService()
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
        fileSystemService
            .Setup(service => service.GetFolderFileCountAndSize(folderPath))
            .Returns(() =>
                new FolderFileCountAndSize(GetRelativeFilePaths().Count, GetTotalBytes())
            );

        var timeProvider = new Mock<TimeProvider>(new ConfigurationBuilder().Build());
        timeProvider.Setup(provider => provider.GetLocalNow()).Returns(Now);

        return new ReleaseFolderVerificationAndExtractionService(
            new SfvChecksumVerifier(progressTracker, NullLogger<SfvChecksumVerifier>.Instance),
            new FolderArchiveExtractionService(
                archiverFactory.Object,
                fileSystemService.Object,
                progressTracker,
                new FolderSizeProgressReporter(),
                NullLogger<FolderArchiveExtractionService>.Instance
            ),
            fileSystemService.Object,
            notificationService,
            timeProvider.Object,
            NullLogger<ReleaseFolderVerificationAndExtractionService>.Instance
        );
    }

    private ReleaseFolderObservation CreateObservation()
    {
        return new ReleaseFolderObservation
        {
            Id = ObservationId,
            FolderPath = folderPath,
            FileCount = 1,
            TotalBytes = 1,
            LastChangedAt = LastChangedAt,
        };
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

    private static string CreateSfvLine(string relativeFilePath, string content)
    {
        return $"{relativeFilePath} {Crc32.HashToUInt32(Encoding.UTF8.GetBytes(content)):X8}";
    }

    private string WriteFile(string relativePath, params string[] lines)
    {
        var filePath = Path.Combine(folderPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, string.Join('\n', lines));

        return filePath;
    }

    private List<string> GetRelativeFilePaths()
    {
        return Directory
            .GetFiles(folderPath, "*", SearchOption.AllDirectories)
            .Select(filePath => Path.GetRelativePath(folderPath, filePath))
            .ToList();
    }

    private long GetTotalBytes()
    {
        return Directory
            .GetFiles(folderPath, "*", SearchOption.AllDirectories)
            .Sum(filePath => new FileInfo(filePath).Length);
    }
}
