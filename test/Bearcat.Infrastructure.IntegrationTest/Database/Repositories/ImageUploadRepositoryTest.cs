using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ImageUploadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime CreatedAt = new(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc);

    private ImageUploadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ImageUploadRepository(DbContext, NoOpSecretProtector.Instance);
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_ReleaseConfigWithCoverAndWithoutUpload_CreatesPendingUpload()
    {
        // Arrange
        var config = await AddReleaseImageUploadConfigAsync(coverUrl: "https://cover.test/1.jpg");

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        var upload = (await GetImageUploadsAsync(config)).ShouldHaveSingleItem();
        upload.UploadState.ShouldBe(UploadState.Pending);
        upload.CreatedAt.ShouldBe(CreatedAt);
        upload.UploadedAt.ShouldBeNull();
        upload.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_CollectionConfigWithCoverAndWithoutUpload_CreatesPendingUpload()
    {
        // Arrange
        var config = await AddCollectionImageUploadConfigAsync(
            coverUrl: "https://cover.test/2.jpg"
        );

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        var upload = (await GetImageUploadsAsync(config)).ShouldHaveSingleItem();
        upload.UploadState.ShouldBe(UploadState.Pending);
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Completed)]
    [TestCase(UploadState.Failed)]
    public async Task CreateMissingImageUploadsAsync_ConfigAlreadyHasUpload_DoesNotCreateAnotherUpload(
        UploadState existingUploadState
    )
    {
        // Arrange
        var config = await AddReleaseImageUploadConfigAsync(
            coverUrl: "https://cover.test/1.jpg",
            existingUploadState: existingUploadState
        );

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        var upload = (await GetImageUploadsAsync(config)).ShouldHaveSingleItem();
        upload.UploadState.ShouldBe(existingUploadState);
        upload.CreatedAt.ShouldBe(CreatedAt.AddDays(-1));
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_ImageHosterRegistrationIsInactive_DoesNotCreateUpload()
    {
        // Arrange
        var config = await AddReleaseImageUploadConfigAsync(
            coverUrl: "https://cover.test/1.jpg",
            imageHosterRegistrationIsActive: false
        );

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        (await GetImageUploadsAsync(config)).ShouldBeEmpty();
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_ReleaseWithoutCover_DoesNotCreateUpload()
    {
        // Arrange
        var config = await AddReleaseImageUploadConfigAsync(coverUrl: null);

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        (await GetImageUploadsAsync(config)).ShouldBeEmpty();
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_SeveralConfigsWithAndWithoutUpload_CreatesUploadsOnlyForConfigsWithoutUpload()
    {
        // Arrange
        var configWithoutUpload = await AddReleaseImageUploadConfigAsync(
            coverUrl: "https://cover.test/1.jpg"
        );
        var configWithUpload = await AddReleaseImageUploadConfigAsync(
            coverUrl: "https://cover.test/2.jpg",
            existingUploadState: UploadState.Completed
        );
        var collectionConfigWithoutUpload = await AddCollectionImageUploadConfigAsync(
            coverUrl: "https://cover.test/3.jpg"
        );

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploadCountByConfigId = await DbContext
            .ImageUploads.GroupBy(upload => upload.ImageUploadConfigId)
            .Select(group => new { ConfigId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.ConfigId, group => group.Count);
        uploadCountByConfigId.ShouldBe(
            new Dictionary<int, int>
            {
                [configWithoutUpload.Id] = 1,
                [configWithUpload.Id] = 1,
                [collectionConfigWithoutUpload.Id] = 1,
            },
            ignoreOrder: true
        );
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_CoverOnlyConfigOfReleaseWithScreenshots_CreatesOnlyCoverUpload()
    {
        // Arrange
        var config = await AddReleaseWithScreenshotsImageUploadConfigAsync(
            uploadCover: true,
            uploadScreenshots: false,
            uploadScreenshotGrid: false
        );

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        var upload = (await GetImageUploadsAsync(config)).ShouldHaveSingleItem();
        upload.ReleaseScreenshotId.ShouldBeNull();
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_ConfigUploadsScreenshotsAndGrid_CreatesPendingUploadPerScreenshot()
    {
        // Arrange
        var config = await AddReleaseWithScreenshotsImageUploadConfigAsync(
            uploadCover: false,
            uploadScreenshots: true,
            uploadScreenshotGrid: true
        );
        var screenshotIds = await DbContext
            .ReleaseScreenshots.Where(screenshot => screenshot.ReleaseId == config.ReleaseId)
            .Select(screenshot => screenshot.Id)
            .ToListAsync();

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        var uploads = await GetImageUploadsAsync(config);
        uploads
            .Select(upload => upload.ReleaseScreenshotId)
            .ShouldBe(screenshotIds.Select(id => (int?)id), ignoreOrder: true);
        uploads.ShouldAllBe(upload => upload.UploadState == UploadState.Pending);
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_ConfigUploadsOnlyScreenshots_SkipsScreenshotGrid()
    {
        // Arrange
        var config = await AddReleaseWithScreenshotsImageUploadConfigAsync(
            uploadCover: true,
            uploadScreenshots: true,
            uploadScreenshotGrid: false
        );
        var screenshotGridId = await DbContext
            .ReleaseScreenshots.Where(screenshot =>
                screenshot.ReleaseId == config.ReleaseId
                && screenshot.Kind == ReleaseScreenshotKind.ScreenshotGrid
            )
            .Select(screenshot => screenshot.Id)
            .SingleAsync();

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        var uploads = await GetImageUploadsAsync(config);
        uploads.Count.ShouldBe(3);
        uploads.ShouldContain(upload => upload.ReleaseScreenshotId == null);
        uploads.ShouldNotContain(upload => upload.ReleaseScreenshotId == screenshotGridId);
    }

    [Test]
    public async Task CreateMissingImageUploadsAsync_ScreenshotFileDeleted_DoesNotCreateUploadForIt()
    {
        // Arrange
        var config = await AddReleaseWithScreenshotsImageUploadConfigAsync(
            uploadCover: false,
            uploadScreenshots: true,
            uploadScreenshotGrid: true
        );
        var deletedScreenshot = await DbContext.ReleaseScreenshots.FirstAsync(screenshot =>
            screenshot.ReleaseId == config.ReleaseId
        );
        deletedScreenshot.FileDeletedAt = CreatedAt;
        await DbContext.SaveChangesAsync();

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);

        // Assert
        var uploads = await GetImageUploadsAsync(config);
        uploads.Count.ShouldBe(2);
        uploads.ShouldNotContain(upload => upload.ReleaseScreenshotId == deletedScreenshot.Id);
    }

    [Test]
    public async Task GetScreenshotsWithFinishedUploadsAsync_AllMatchingConfigsCompleted_ReturnsScreenshots()
    {
        // Arrange
        var config = await AddReleaseWithScreenshotsImageUploadConfigAsync(
            uploadCover: false,
            uploadScreenshots: true,
            uploadScreenshotGrid: true
        );
        await AddScreenshotUploadsAsync(config, UploadState.Completed);

        // Act
        var result = await repository.GetScreenshotsWithFinishedUploadsAsync(
            CancellationToken.None
        );

        // Assert
        result.Count.ShouldBe(3);
    }

    [Test]
    public async Task GetScreenshotsWithFinishedUploadsAsync_OtherMatchingConfigStillPending_ReturnsNoScreenshots()
    {
        // Arrange
        var config = await AddReleaseWithScreenshotsImageUploadConfigAsync(
            uploadCover: false,
            uploadScreenshots: true,
            uploadScreenshotGrid: true
        );
        await AddScreenshotUploadsAsync(config, UploadState.Completed);
        var otherConfig = await AddImageUploadConfigToReleaseAsync(config.ReleaseId!.Value);
        await AddScreenshotUploadsAsync(otherConfig, UploadState.Pending);

        // Act
        var result = await repository.GetScreenshotsWithFinishedUploadsAsync(
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetScreenshotsWithFinishedUploadsAsync_UploadFailed_ReturnsNoScreenshots()
    {
        // Arrange
        var config = await AddReleaseWithScreenshotsImageUploadConfigAsync(
            uploadCover: false,
            uploadScreenshots: true,
            uploadScreenshotGrid: true
        );
        await AddScreenshotUploadsAsync(config, UploadState.Failed);

        // Act
        var result = await repository.GetScreenshotsWithFinishedUploadsAsync(
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetScreenshotsWithFinishedUploadsAsync_NoConfigUploadsScreenshots_ReturnsScreenshotsImmediately()
    {
        // Arrange
        var config = await AddReleaseWithScreenshotsImageUploadConfigAsync(
            uploadCover: true,
            uploadScreenshots: false,
            uploadScreenshotGrid: false
        );

        // Act
        var result = await repository.GetScreenshotsWithFinishedUploadsAsync(
            CancellationToken.None
        );

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldAllBe(screenshot => screenshot.ReleaseId == config.ReleaseId);
    }

    [Test]
    public async Task GetLeastRecentlyCheckedImageUploadsAsync_UploadsOfSeveralStatesAndRegistrations_ReturnsOnlyDueCompletedUploadsOfRegistration()
    {
        // Arrange
        var checkedBefore = CreatedAt.AddHours(-6);
        var config = await AddReleaseImageUploadConfigAsync(coverUrl: null);
        var otherRegistrationConfig = await AddReleaseImageUploadConfigAsync(coverUrl: null);
        var neverCheckedUpload = await AddImageUploadAsync(config, onlineCheckedAt: null);
        var checkedLongAgoUpload = await AddImageUploadAsync(
            config,
            onlineCheckedAt: checkedBefore.AddMinutes(-1)
        );
        await AddImageUploadAsync(config, onlineCheckedAt: checkedBefore.AddMinutes(1));
        await AddImageUploadAsync(otherRegistrationConfig, onlineCheckedAt: null);
        await AddImageUploadAsync(config, onlineCheckedAt: null, uploadState: UploadState.Failed);
        await AddImageUploadAsync(config, onlineCheckedAt: null, uploadState: UploadState.Pending);

        await AddImageUploadAsync(
            config,
            onlineCheckedAt: null,
            uploadedAt: checkedBefore.AddMinutes(1)
        );

        // Act
        var result = await repository.GetLeastRecentlyCheckedImageUploadsAsync(
            imageHosterRegistrationId: config.ImageHosterRegistrationId,
            checkedBefore: checkedBefore,
            maximumImageUploadCount: 10,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(upload => upload.Id)
            .ShouldBe([neverCheckedUpload.Id, checkedLongAgoUpload.Id], ignoreOrder: true);
        result.ShouldAllBe(upload =>
            upload.ImageUploadConfig.ImageHosterRegistration.Id == config.ImageHosterRegistrationId
        );
        result.ShouldAllBe(upload => upload.ImageUrls.Count == 1);
    }

    [Test]
    public async Task GetLeastRecentlyCheckedImageUploadsAsync_MoreUploadsThanMaximum_ReturnsNeverCheckedFirstThenOldestChecked()
    {
        // Arrange
        var config = await AddReleaseImageUploadConfigAsync(coverUrl: null);
        var checkedSecondOldestUpload = await AddImageUploadAsync(
            config,
            onlineCheckedAt: CreatedAt.AddDays(-2)
        );
        await AddImageUploadAsync(config, onlineCheckedAt: CreatedAt.AddDays(-1));
        var checkedOldestUpload = await AddImageUploadAsync(
            config,
            onlineCheckedAt: CreatedAt.AddDays(-3)
        );
        var neverCheckedUpload = await AddImageUploadAsync(config, onlineCheckedAt: null);

        // Act
        var result = await repository.GetLeastRecentlyCheckedImageUploadsAsync(
            imageHosterRegistrationId: config.ImageHosterRegistrationId,
            checkedBefore: CreatedAt,
            maximumImageUploadCount: 3,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result
            .Select(upload => upload.Id)
            .ShouldBe([
                neverCheckedUpload.Id,
                checkedOldestUpload.Id,
                checkedSecondOldestUpload.Id,
            ]);
    }

    [Test]
    public async Task ResetToPendingForReupload_CheckedCoverUploadSaved_DeletesUrlsAndReturnsUploadAsPending()
    {
        // Arrange
        var config = await AddReleaseImageUploadConfigAsync(coverUrl: "https://cover.test/1.jpg");
        var coverUpload = await AddImageUploadAsync(config, onlineCheckedAt: null);
        var checkedUpload = (
            await repository.GetLeastRecentlyCheckedImageUploadsAsync(
                imageHosterRegistrationId: config.ImageHosterRegistrationId,
                checkedBefore: CreatedAt,
                maximumImageUploadCount: 10,
                cancellationToken: CancellationToken.None
            )
        ).ShouldHaveSingleItem();

        // Act
        checkedUpload.ResetToPendingForReupload();
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (
            await DbContext.ImageUploadUrls.CountAsync(url => url.ImageUploadId == coverUpload.Id)
        ).ShouldBe(0);
        var pendingUpload = (
            await repository.GetPendingImageUploadsAsync(CancellationToken.None)
        ).ShouldHaveSingleItem();
        pendingUpload.Id.ShouldBe(coverUpload.Id);
        pendingUpload.ImageUrls.ShouldBeEmpty();
        pendingUpload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    private async Task<ImageUpload> AddImageUploadAsync(
        ImageUploadConfig config,
        DateTime? onlineCheckedAt,
        UploadState uploadState = UploadState.Completed,
        DateTime? uploadedAt = null
    )
    {
        var upload = new ImageUpload
        {
            ImageUploadConfigId = config.Id,
            CreatedAt = CreatedAt.AddDays(-10),
            UploadState = uploadState,
            UploadedAt = uploadedAt,
            OnlineCheckedAt = onlineCheckedAt,
            ErrorMessages = [],
            ImageUrls =
            [
                new ImageUploadUrl
                {
                    ImageSize = ImageSize.Full,
                    Url = $"https://images.test/{Guid.NewGuid():N}.jpg",
                },
            ],
        };

        DbContext.ImageUploads.Add(upload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return upload;
    }

    private async Task<List<ImageUpload>> GetImageUploadsAsync(ImageUploadConfig config)
    {
        DbContext.ChangeTracker.Clear();

        return await DbContext
            .ImageUploads.Where(upload => upload.ImageUploadConfigId == config.Id)
            .ToListAsync();
    }

    private async Task<ImageUploadConfig> AddReleaseImageUploadConfigAsync(
        string? coverUrl,
        UploadState? existingUploadState = null,
        bool imageHosterRegistrationIsActive = true
    )
    {
        var releaseNumber = await DbContext.Releases.CountAsync() + 1;
        var release = new Release
        {
            Name = $"Bodies.2023.S01E{releaseNumber:00}-GRP",
            CreatedAt = CreatedAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/bodies",
            ReleaseGroup = CreateReleaseGroup(),
            Metadata = new ReleaseMetadata
            {
                MetadataDatabaseClassName = ReleaseMetadata.ManualSource,
                Title = "Bodies",
                CoverUrl = coverUrl,
            },
        };
        var config = new ImageUploadConfig
        {
            Release = release,
            Name = "Cover",
            ImageHosterRegistration = CreateImageHosterRegistration(
                imageHosterRegistrationIsActive
            ),
            ImageUploads = CreateExistingUploads(existingUploadState),
        };

        DbContext.ImageUploadConfigs.Add(config);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return config;
    }

    private async Task<ImageUploadConfig> AddReleaseWithScreenshotsImageUploadConfigAsync(
        bool uploadCover,
        bool uploadScreenshots,
        bool uploadScreenshotGrid,
        bool imageHosterRegistrationIsActive = true
    )
    {
        var release = new Release
        {
            Name = "Bodies.2023.S01E01-GRP",
            CreatedAt = CreatedAt,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/bodies",
            ReleaseGroup = CreateReleaseGroup(),
            CreateScreenshots = true,
            CreateScreenshotGrid = true,
            ScreenshotsGeneratedAt = CreatedAt,
            Metadata = new ReleaseMetadata
            {
                MetadataDatabaseClassName = ReleaseMetadata.ManualSource,
                Title = "Bodies",
                CoverUrl = "https://cover.test/1.jpg",
            },
            Screenshots =
            [
                CreateScreenshot(ReleaseScreenshotKind.Screenshot, index: 1),
                CreateScreenshot(ReleaseScreenshotKind.Screenshot, index: 2),
                CreateScreenshot(ReleaseScreenshotKind.ScreenshotGrid, index: 1),
            ],
        };
        var config = new ImageUploadConfig
        {
            Release = release,
            Name = "Screens",
            ImageHosterRegistration = CreateImageHosterRegistration(
                imageHosterRegistrationIsActive
            ),
            UploadCover = uploadCover,
            UploadScreenshots = uploadScreenshots,
            UploadScreenshotGrid = uploadScreenshotGrid,
            ImageUploads = [],
        };

        DbContext.ImageUploadConfigs.Add(config);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return config;
    }

    private async Task AddScreenshotUploadsAsync(ImageUploadConfig config, UploadState uploadState)
    {
        var screenshotIds = await DbContext
            .ReleaseScreenshots.Where(screenshot => screenshot.ReleaseId == config.ReleaseId)
            .Select(screenshot => screenshot.Id)
            .ToListAsync();

        DbContext.ImageUploads.AddRange(
            screenshotIds.Select(screenshotId => new ImageUpload
            {
                ImageUploadConfigId = config.Id,
                ReleaseScreenshotId = screenshotId,
                CreatedAt = CreatedAt,
                UploadState = uploadState,
                ErrorMessages = [],
                ImageUrls = [],
            })
        );

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task<ImageUploadConfig> AddImageUploadConfigToReleaseAsync(int releaseId)
    {
        var config = new ImageUploadConfig
        {
            ReleaseId = releaseId,
            Name = "Other screens",
            ImageHosterRegistration = CreateImageHosterRegistration(isActive: true),
            UploadCover = false,
            UploadScreenshots = true,
            UploadScreenshotGrid = true,
            ImageUploads = [],
        };

        DbContext.ImageUploadConfigs.Add(config);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return config;
    }

    private static ReleaseScreenshot CreateScreenshot(ReleaseScreenshotKind kind, int index)
    {
        return new ReleaseScreenshot
        {
            Kind = kind,
            Index = index,
            RelativeFilePath = $"screenshots/1/{kind}-{index}.jpg",
            Width = 1920,
            Height = 1080,
            SizeBytes = 1024,
            CreatedAt = CreatedAt,
        };
    }

    private async Task<ImageUploadConfig> AddCollectionImageUploadConfigAsync(string? coverUrl)
    {
        var collection = new ReleaseCollection
        {
            ReleaseGroup = CreateReleaseGroup(),
            Key = $"key-{Guid.NewGuid():N}",
            Name = "Bodies.2023.S01",
            CreatedAt = CreatedAt,
            Metadata = new ReleaseCollectionMetadata
            {
                MetadataDatabaseClassName = "TvdbMetadataDatabase",
                Title = "Bodies",
                CoverUrl = coverUrl,
            },
        };
        var config = new ImageUploadConfig
        {
            ReleaseCollection = collection,
            Name = "Collection cover",
            ImageHosterRegistration = CreateImageHosterRegistration(isActive: true),
            ImageUploads = [],
        };

        DbContext.ImageUploadConfigs.Add(config);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return config;
    }

    private static List<ImageUpload> CreateExistingUploads(UploadState? existingUploadState)
    {
        return existingUploadState is null
            ? []
            :
            [
                new ImageUpload
                {
                    CreatedAt = CreatedAt.AddDays(-1),
                    UploadState = existingUploadState.Value,
                    ErrorMessages = [],
                    ImageUrls = [],
                },
            ];
    }

    private static ReleaseGroup CreateReleaseGroup()
    {
        return new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
    }

    private static ImageHosterRegistration CreateImageHosterRegistration(bool isActive)
    {
        return new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = isActive,
        };
    }
}
