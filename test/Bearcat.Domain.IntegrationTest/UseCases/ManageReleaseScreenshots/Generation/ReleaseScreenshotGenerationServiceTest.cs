using Bearcat.Abstractions.Media;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Generation;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Storage;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseScreenshots.Generation;

public class ReleaseScreenshotGenerationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime GeneratedAt = new(
        2026,
        11,
        15,
        20,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private string tempRootPath = null!;
    private string releaseFolderPath = null!;
    private ReleaseScreenshotDirectory screenshotDirectory = null!;
    private Mock<IVideoScreenshotCreator> videoScreenshotCreatorMock = null!;
    private ReleaseScreenshotGenerationService service = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        releaseFolderPath = Path.Combine(tempRootPath, "releases", "Movie.2025.2160p");
        Directory.CreateDirectory(releaseFolderPath);
        screenshotDirectory = new ReleaseScreenshotDirectory(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Bearcat:DataDirectory"] = Path.Combine(tempRootPath, "data"),
                    }
                )
                .Build()
        );
        videoScreenshotCreatorMock = new Mock<IVideoScreenshotCreator>(MockBehavior.Strict);
        var mediaMetadataService = new MediaMetadataService(
            new MediaMetadataRepository(DbContext),
            new Mock<IMediaMetadataExtractor>(MockBehavior.Strict).Object,
            new FileSystemService(),
            new ReleaseClassificationService(
                new ReleaseClassificationRepository(DbContext),
                new ControllableTimeProvider(GeneratedAt),
                NullLogger<ReleaseClassificationService>.Instance
            ),
            new ControllableTimeProvider(GeneratedAt),
            NullLogger<MediaMetadataService>.Instance
        );

        service = new ReleaseScreenshotGenerationService(
            new ReleaseScreenshotGenerationRepository(DbContext),
            videoScreenshotCreatorMock.Object,
            mediaMetadataService,
            new FileSystemService(),
            screenshotDirectory,
            new ControllableTimeProvider(GeneratedAt),
            NullLogger<ReleaseScreenshotGenerationService>.Instance
        );
    }

    [TearDown]
    public void DeleteTempRootPath()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessPendingAsync_ScreenshotsAndGridEnabled_CreatesFromLargestVideoAndStoresScreenshots()
    {
        // Arrange
        var release = await AddReleaseAsync(transferCharacteristics: "PQ", hdrFormat: null);
        var screenshotDirectoryPath = screenshotDirectory.GetDirectoryPath(release.Id);
        ScreenshotCreationRequest? screenshotRequest = null;
        ScreenshotGridCreationRequest? screenshotGridRequest = null;
        videoScreenshotCreatorMock
            .Setup(creator =>
                creator.CreateScreenshotsAsync(
                    It.IsAny<ScreenshotCreationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ScreenshotCreationRequest request, CancellationToken _) =>
                    screenshotRequest = request
            )
            .ReturnsAsync([
                new CreatedScreenshotFile(
                    Path.Combine(screenshotDirectoryPath, "screenshot-01.jpg"),
                    TimeSpan.FromMinutes(10),
                    1920,
                    1080,
                    250_000
                ),
                new CreatedScreenshotFile(
                    Path.Combine(screenshotDirectoryPath, "screenshot-02.jpg"),
                    TimeSpan.FromMinutes(20),
                    1920,
                    1080,
                    260_000
                ),
            ]);
        videoScreenshotCreatorMock
            .Setup(creator =>
                creator.CreateScreenshotGridAsync(
                    It.IsAny<ScreenshotGridCreationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ScreenshotGridCreationRequest request, CancellationToken _) =>
                    screenshotGridRequest = request
            )
            .ReturnsAsync(
                new CreatedScreenshotGridFile(
                    Path.Combine(screenshotDirectoryPath, "screenshot-grid.jpg"),
                    1920,
                    1400,
                    900_000
                )
            );

        // Act
        await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        var videoFilePath = Path.Combine(releaseFolderPath, "movie.mkv");
        screenshotRequest.ShouldBe(
            new ScreenshotCreationRequest(
                VideoFilePath: videoFilePath,
                Duration: TimeSpan.FromSeconds(3600),
                TransferCharacteristic: VideoTransferCharacteristic.PerceptualQuantizer,
                Count: 2,
                OutputDirectory: screenshotDirectoryPath
            )
        );
        screenshotGridRequest.ShouldNotBeNull();
        screenshotGridRequest.VideoFilePath.ShouldBe(videoFilePath);
        screenshotGridRequest.Columns.ShouldBe(3);
        screenshotGridRequest.Rows.ShouldBe(5);
        screenshotGridRequest.GridWidthPixels.ShouldBe(1920);
        screenshotGridRequest.HeaderLines[0].ShouldBe("movie.mkv");
        screenshotGridRequest.OutputFilePath.ShouldBe(
            Path.Combine(screenshotDirectoryPath, "screenshot-grid.jpg")
        );

        await using var assertContext = CreateDbContext();
        var storedRelease = await assertContext
            .Releases.Include(r => r.Screenshots)
            .SingleAsync(r => r.Id == release.Id);
        storedRelease.ScreenshotsGeneratedAt.ShouldBe(GeneratedAt);
        storedRelease.ScreenshotGenerationErrorMessage.ShouldBeNull();
        storedRelease
            .Screenshots.OrderBy(screenshot => screenshot.Kind)
            .ThenBy(screenshot => screenshot.Index)
            .Select(screenshot => new
            {
                screenshot.Kind,
                screenshot.Index,
                screenshot.Timestamp,
                screenshot.RelativeFilePath,
                screenshot.SizeBytes,
            })
            .ShouldBe([
                new
                {
                    Kind = ReleaseScreenshotKind.Screenshot,
                    Index = 1,
                    Timestamp = (TimeSpan?)TimeSpan.FromMinutes(10),
                    RelativeFilePath = $"screenshots/{release.Id}/screenshot-01.jpg",
                    SizeBytes = 250_000L,
                },
                new
                {
                    Kind = ReleaseScreenshotKind.Screenshot,
                    Index = 2,
                    Timestamp = (TimeSpan?)TimeSpan.FromMinutes(20),
                    RelativeFilePath = $"screenshots/{release.Id}/screenshot-02.jpg",
                    SizeBytes = 260_000L,
                },
                new
                {
                    Kind = ReleaseScreenshotKind.ScreenshotGrid,
                    Index = 1,
                    Timestamp = (TimeSpan?)null,
                    RelativeFilePath = $"screenshots/{release.Id}/screenshot-grid.jpg",
                    SizeBytes = 900_000L,
                },
            ]);
    }

    [Test]
    public async Task ProcessPendingAsync_ScreenshotGridCreationFails_StoresErrorMessageAndDeletesPartialFiles()
    {
        // Arrange
        var release = await AddReleaseAsync(transferCharacteristics: "BT.709", hdrFormat: null);
        var screenshotDirectoryPath = screenshotDirectory.GetDirectoryPath(release.Id);
        var screenshotFilePath = Path.Combine(screenshotDirectoryPath, "screenshot-01.jpg");
        videoScreenshotCreatorMock
            .Setup(creator =>
                creator.CreateScreenshotsAsync(
                    It.IsAny<ScreenshotCreationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() =>
            {
                Directory.CreateDirectory(screenshotDirectoryPath);
                File.WriteAllText(screenshotFilePath, "jpeg");
            })
            .ReturnsAsync([
                new CreatedScreenshotFile(
                    screenshotFilePath,
                    TimeSpan.FromMinutes(10),
                    1920,
                    1080,
                    4
                ),
            ]);
        videoScreenshotCreatorMock
            .Setup(creator =>
                creator.CreateScreenshotGridAsync(
                    It.IsAny<ScreenshotGridCreationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("ffmpeg failed: invalid data"));

        // Act
        await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        await using var assertContext = CreateDbContext();
        var storedRelease = await assertContext
            .Releases.Include(r => r.Screenshots)
            .SingleAsync(r => r.Id == release.Id);
        storedRelease.ScreenshotGenerationErrorMessage.ShouldBe("ffmpeg failed: invalid data");
        storedRelease.ScreenshotsGeneratedAt.ShouldBeNull();
        storedRelease.Screenshots.ShouldBeEmpty();
        Directory.Exists(screenshotDirectoryPath).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessPendingAsync_DolbyVisionWithoutBaseLayer_StoresErrorMessageWithoutCreatingScreenshots()
    {
        // Arrange
        var release = await AddReleaseAsync(
            transferCharacteristics: null,
            hdrFormat: "Dolby Vision, Version 1.0, Profile 5"
        );

        // Act
        await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        await using var assertContext = CreateDbContext();
        var storedRelease = await assertContext.Releases.SingleAsync(r => r.Id == release.Id);
        storedRelease.ScreenshotGenerationErrorMessage.ShouldBe(
            "Dolby Vision without an HDR10 or HLG base layer is not supported."
        );
        videoScreenshotCreatorMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessPendingAsync_VideoFileMissing_StoresErrorMessageWithoutCreatingScreenshots()
    {
        // Arrange
        var release = await AddReleaseAsync(transferCharacteristics: "BT.709", hdrFormat: null);
        File.Delete(Path.Combine(releaseFolderPath, "movie.mkv"));

        // Act
        await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        await using var assertContext = CreateDbContext();
        var storedRelease = await assertContext.Releases.SingleAsync(r => r.Id == release.Id);
        storedRelease.ScreenshotGenerationErrorMessage.ShouldBe(
            $"The video file {Path.Combine(releaseFolderPath, "movie.mkv")} does not exist."
        );
        videoScreenshotCreatorMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessPendingAsync_MediaMetadataNotExtracted_ExtractsMetadataBeforeCreatingScreenshots()
    {
        // Arrange
        var release = new Release
        {
            Name = "Movie.2025.2160p",
            CreatedAt = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Unspecified),
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Movie group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
            CreateScreenshots = true,
            ScreenshotCount = 2,
            ImageUploadConfigs =
            [
                CreateImageUploadConfig(uploadScreenshots: true, uploadScreenshotGrid: false),
            ],
        };

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        await using var assertContext = CreateDbContext();
        var storedRelease = await assertContext.Releases.SingleAsync(r => r.Id == release.Id);
        storedRelease.MediaMetadataExtractedAt.ShouldBe(GeneratedAt);
        storedRelease.ScreenshotGenerationErrorMessage.ShouldBe(
            $"No video file was found in the release folder {releaseFolderPath}."
        );
        videoScreenshotCreatorMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessPendingAsync_ImageUploadConfigUploadsOnlyScreenshotGrid_CreatesOnlyScreenshotGrid()
    {
        // Arrange
        var release = await AddReleaseAsync(
            transferCharacteristics: "BT.709",
            hdrFormat: null,
            uploadScreenshots: false,
            uploadScreenshotGrid: true
        );
        var screenshotGridFilePath = Path.Combine(
            screenshotDirectory.GetDirectoryPath(release.Id),
            "screenshot-grid.jpg"
        );
        videoScreenshotCreatorMock
            .Setup(creator =>
                creator.CreateScreenshotGridAsync(
                    It.IsAny<ScreenshotGridCreationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new CreatedScreenshotGridFile(screenshotGridFilePath, 1920, 2400, 900_000)
            );

        // Act
        await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        await using var assertContext = CreateDbContext();
        var storedRelease = await assertContext
            .Releases.Include(r => r.Screenshots)
            .SingleAsync(r => r.Id == release.Id);
        storedRelease.ScreenshotsGeneratedAt.ShouldBe(GeneratedAt);
        storedRelease
            .Screenshots.ShouldHaveSingleItem()
            .Kind.ShouldBe(ReleaseScreenshotKind.ScreenshotGrid);
        videoScreenshotCreatorMock.Verify(
            creator =>
                creator.CreateScreenshotGridAsync(
                    It.IsAny<ScreenshotGridCreationRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        videoScreenshotCreatorMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ScheduleRegenerationAsync_ReleaseWithScreenshots_RemovesScreenshotsAndFilesAndResetsState()
    {
        // Arrange
        var release = await AddReleaseAsync(transferCharacteristics: "BT.709", hdrFormat: null);
        var screenshotDirectoryPath = screenshotDirectory.GetDirectoryPath(release.Id);
        Directory.CreateDirectory(screenshotDirectoryPath);
        await File.WriteAllTextAsync(
            Path.Combine(screenshotDirectoryPath, "screenshot-01.jpg"),
            "jpeg"
        );
        var storedRelease = await DbContext.Releases.SingleAsync(r => r.Id == release.Id);
        storedRelease.ScreenshotsGeneratedAt = GeneratedAt;
        storedRelease.Screenshots.Add(
            new ReleaseScreenshot
            {
                Kind = ReleaseScreenshotKind.Screenshot,
                Index = 1,
                Timestamp = TimeSpan.FromMinutes(10),
                RelativeFilePath = $"screenshots/{release.Id}/screenshot-01.jpg",
                Width = 1920,
                Height = 1080,
                SizeBytes = 4,
                CreatedAt = GeneratedAt,
            }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await service.ScheduleRegenerationAsync(release.Id, CancellationToken.None);

        // Assert
        await using var assertContext = CreateDbContext();
        var result = await assertContext
            .Releases.Include(r => r.Screenshots)
            .SingleAsync(r => r.Id == release.Id);
        result.ScreenshotsGeneratedAt.ShouldBeNull();
        result.ScreenshotGenerationErrorMessage.ShouldBeNull();
        result.Screenshots.ShouldBeEmpty();
        Directory.Exists(screenshotDirectoryPath).ShouldBeFalse();
    }

    private async Task<Release> AddReleaseAsync(
        string? transferCharacteristics,
        string? hdrFormat,
        bool uploadScreenshots = true,
        bool uploadScreenshotGrid = true
    )
    {
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "video");

        var release = new Release
        {
            Name = "Movie.2025.2160p",
            CreatedAt = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Unspecified),
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Movie group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
            MediaMetadataExtractedAt = GeneratedAt,
            CreateScreenshots = true,
            ScreenshotCount = 2,
            CreateScreenshotGrid = true,
            ScreenshotGridColumns = 3,
            ScreenshotGridRows = 5,
            MediaFiles =
            [
                new ReleaseMediaFile
                {
                    RelativePath = Path.Combine("Sample", "sample.mkv"),
                    SizeBytes = 10_000,
                    MediaInfoJson = CreateMediaInfoJson(transferCharacteristics, hdrFormat),
                    MediaInfoText = "General",
                },
                new ReleaseMediaFile
                {
                    RelativePath = "movie.mkv",
                    SizeBytes = 8_000_000_000,
                    MediaInfoJson = CreateMediaInfoJson(transferCharacteristics, hdrFormat),
                    MediaInfoText = "General",
                },
            ],
            ImageUploadConfigs =
            [
                CreateImageUploadConfig(
                    uploadScreenshots: uploadScreenshots,
                    uploadScreenshotGrid: uploadScreenshotGrid
                ),
            ],
        };

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }

    private static ImageUploadConfig CreateImageUploadConfig(
        bool uploadScreenshots,
        bool uploadScreenshotGrid
    )
    {
        return new ImageUploadConfig
        {
            Name = "Screens",
            ImageHosterRegistration = new ImageHosterRegistration
            {
                Name = "PiXhost",
                ImageHosterClassName = "PixHost",
                SerializedConfig = "{}",
                IsActive = true,
            },
            UploadCover = false,
            UploadScreenshots = uploadScreenshots,
            UploadScreenshotGrid = uploadScreenshotGrid,
        };
    }

    private static string CreateMediaInfoJson(string? transferCharacteristics, string? hdrFormat)
    {
        var transferCharacteristicsProperty = transferCharacteristics is null
            ? string.Empty
            : $", \"transfer_characteristics\": \"{transferCharacteristics}\"";
        var hdrFormatProperty = hdrFormat is null
            ? string.Empty
            : $", \"HDR_Format\": \"{hdrFormat}\"";

        return $$$"""
            {"media": {"track": [
              {"@type": "General", "FileSize": "8000000000", "Duration": "3600.000"},
              {"@type": "Video", "Format": "HEVC", "Width": "3840", "Height": "2160", "FrameRate": "23.976"{{{transferCharacteristicsProperty}}}{{{hdrFormatProperty}}}}
            ]}}
            """;
    }
}
