using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploads;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Storage;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageImageUploads;

public class ImageUploadServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string ImageHosterClassName = "ImgBb";

    private string tempRootPath = null!;
    private ReleaseScreenshotDirectory screenshotDirectory = null!;
    private Mock<IImageHoster> imageHosterMock = null!;
    private ImageUploadService service = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        screenshotDirectory = new ReleaseScreenshotDirectory(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Bearcat:DataDirectory"] = tempRootPath }
                )
                .Build()
        );

        imageHosterMock = new Mock<IImageHoster>();
        imageHosterMock
            .Setup(hoster => hoster.DeserializeConfig(It.IsAny<string>()))
            .Returns(Mock.Of<IImageHosterConfig>());

        var imageHosterFactoryMock = new Mock<IImageHosterFactory>();
        imageHosterFactoryMock
            .Setup(factory => factory.GetByClassName())
            .Returns(
                new Dictionary<string, IImageHoster>
                {
                    [ImageHosterClassName] = imageHosterMock.Object,
                }
            );

        service = new ImageUploadService(
            new ImageUploadRepository(DbContext, NoOpSecretProtector.Instance),
            imageHosterFactoryMock.Object,
            screenshotDirectory,
            new FileSystemService(),
            CreateTimeProvider(),
            NullLogger<ImageUploadService>.Instance
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
    public async Task ProcessAsync_CollectionWithCover_UploadsCoverAndStoresUrls()
    {
        // Arrange
        SetupSuccessfulUpload();
        var collection = await AddCollectionWithImageConfigAsync(
            "https://artworks.example/cover.jpg"
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var imageUpload = await DbContext
            .ImageUploads.Include(upload => upload.ImageUrls)
            .SingleAsync(upload => upload.ImageUploadConfig.ReleaseCollectionId == collection.Id);

        imageUpload.UploadState.ShouldBe(UploadState.Completed);
        imageUpload.ImageUrls.Select(url => url.Url).ShouldBe(["https://img.example/full.jpg"]);

        imageHosterMock.Verify(
            hoster =>
                hoster.UploadImageAsync(
                    It.Is<ImageToUploadDto>(image =>
                        image.Source == "https://artworks.example/cover.jpg"
                        && image.Name == collection.Name
                    ),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_ImageHosterRegistrationUsesSpecificProxyServer_UploadsImageInsideImageHosterProxyScope()
    {
        // Arrange
        ProxyCategoryScopeState? stateDuringUpload = null;
        imageHosterMock
            .Setup(hoster =>
                hoster.UploadImageAsync(
                    It.IsAny<ImageToUploadDto>(),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => stateDuringUpload = ProxyCategoryScope.Current)
            .ReturnsAsync(
                (ImageToUploadDto image, IImageHosterConfig _, CancellationToken _) =>
                    new UploadImageResult(
                        IsSuccess: true,
                        Image: image,
                        ImageUrls: [new ImageUrl(ImageSize.Full, "https://img.example/full.jpg")],
                        ErrorMessages: []
                    )
            );
        await AddCollectionWithImageConfigAsync("https://artworks.example/cover.jpg");
        var proxyServer = new ProxyServer
        {
            Name = "Image proxy",
            ProxyType = ProxyType.Http,
            Host = "proxy.example.com",
            Port = 8080,
        };
        DbContext.ProxyServers.Add(proxyServer);
        await DbContext.SaveChangesAsync();
        var registration = await DbContext.ImageHosterRegistrations.SingleAsync();
        registration.ProxySelection = ProxySelection.SpecificProxyServer;
        registration.ProxyServerId = proxyServer.Id;
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        stateDuringUpload.ShouldBe(
            new ProxyCategoryScopeState(
                ProxyCategory.ImageHosters,
                ProxySelection.SpecificProxyServer,
                proxyServer.Id
            )
        );
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    [Test]
    public async Task ProcessAsync_CollectionWithoutCover_DoesNotCreateUpload()
    {
        // Arrange
        var collection = await AddCollectionWithImageConfigAsync(coverUrl: null);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var hasUpload = await DbContext.ImageUploads.AnyAsync(upload =>
            upload.ImageUploadConfig.ReleaseCollectionId == collection.Id
        );
        hasUpload.ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_ReleaseConfigAlongsideCollection_StillUploadsRelease()
    {
        // Arrange
        SetupSuccessfulUpload();
        await AddCollectionWithImageConfigAsync("https://artworks.example/cover.jpg");
        var release = await AddReleaseWithImageConfigAsync("https://artworks.example/release.jpg");

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var imageUpload = await DbContext.ImageUploads.SingleAsync(upload =>
            upload.ImageUploadConfig.ReleaseId == release.Id
        );
        imageUpload.UploadState.ShouldBe(UploadState.Completed);
    }

    [Test]
    public async Task ProcessAsync_PendingUploadWithoutCover_MarksFailed()
    {
        // Arrange
        var imageUpload = await AddPendingUploadForReleaseWithoutCoverAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var persisted = await DbContext.ImageUploads.SingleAsync(upload =>
            upload.Id == imageUpload.Id
        );
        persisted.UploadState.ShouldBe(UploadState.Failed);
        persisted.ErrorMessages.ShouldBe(["Image upload source has no cover URL."]);
        imageHosterMock.Verify(
            hoster =>
                hoster.UploadImageAsync(
                    It.IsAny<ImageToUploadDto>(),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_UploadReturnsFailure_MarksFailedWithErrorMessages()
    {
        // Arrange
        imageHosterMock
            .Setup(hoster =>
                hoster.UploadImageAsync(
                    It.IsAny<ImageToUploadDto>(),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ImageToUploadDto image, IImageHosterConfig _, CancellationToken _) =>
                    new UploadImageResult(
                        IsSuccess: false,
                        Image: image,
                        ImageUrls: [],
                        ErrorMessages: ["upload rejected"]
                    )
            );
        var collection = await AddCollectionWithImageConfigAsync(
            "https://artworks.example/cover.jpg"
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var imageUpload = await DbContext.ImageUploads.SingleAsync(upload =>
            upload.ImageUploadConfig.ReleaseCollectionId == collection.Id
        );
        imageUpload.UploadState.ShouldBe(UploadState.Failed);
        imageUpload.ErrorMessages.ShouldBe(["upload rejected"]);
    }

    [Test]
    public async Task ProcessAsync_UploadThrows_MarksFailedWithInnerExceptionMessage()
    {
        // Arrange
        imageHosterMock
            .Setup(hoster =>
                hoster.UploadImageAsync(
                    It.IsAny<ImageToUploadDto>(),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new InvalidOperationException("outer", new InvalidOperationException("inner cause"))
            );
        var collection = await AddCollectionWithImageConfigAsync(
            "https://artworks.example/cover.jpg"
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var imageUpload = await DbContext.ImageUploads.SingleAsync(upload =>
            upload.ImageUploadConfig.ReleaseCollectionId == collection.Id
        );
        imageUpload.UploadState.ShouldBe(UploadState.Failed);
        imageUpload.ErrorMessages.ShouldBe(["inner cause"]);
    }

    [Test]
    public async Task DeleteAsync_CompletedCollectionUpload_IsUploadedAgainOnNextProcessing()
    {
        // Arrange
        SetupSuccessfulUpload();
        var collection = await AddCollectionWithImageConfigAsync(
            "https://artworks.example/cover.jpg"
        );
        await service.ProcessAsync(CancellationToken.None);
        var deletedUpload = await DbContext.ImageUploads.SingleAsync(upload =>
            upload.ImageUploadConfig.ReleaseCollectionId == collection.Id
        );

        // Act
        await service.DeleteAsync(deletedUpload.Id);
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var imageUpload = await DbContext.ImageUploads.SingleAsync(upload =>
            upload.ImageUploadConfig.ReleaseCollectionId == collection.Id
        );
        imageUpload.Id.ShouldNotBe(deletedUpload.Id);
        imageUpload.UploadState.ShouldBe(UploadState.Completed);
        imageHosterMock.Verify(
            hoster =>
                hoster.UploadImageAsync(
                    It.IsAny<ImageToUploadDto>(),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Test]
    public async Task ProcessAsync_ConfigUploadsScreenshotsAndGrid_UploadsScreenshotFilesAsLocalFiles()
    {
        // Arrange
        SetupSuccessfulUpload();
        var release = await AddReleaseWithScreenshotImageConfigAsync(writeScreenshotFiles: true);
        var screenshotFilePath = screenshotDirectory.GetFilePath(
            ReleaseScreenshotDirectory.GetRelativeFilePath(release.Id, "screenshot-01.jpg")
        );
        var screenshotGridFilePath = screenshotDirectory.GetFilePath(
            ReleaseScreenshotDirectory.GetRelativeFilePath(release.Id, "screenshot-grid.jpg")
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var imageUploads = await DbContext.ImageUploads.ToListAsync();
        imageUploads.Count.ShouldBe(2);
        imageUploads.ShouldAllBe(upload =>
            upload.UploadState == UploadState.Completed && upload.ReleaseScreenshotId != null
        );
        (await DbContext.ReleaseScreenshots.ToListAsync()).ShouldAllBe(screenshot =>
            screenshot.FileDeletedAt != null
        );
        File.Exists(screenshotFilePath).ShouldBeFalse();
        Directory.Exists(screenshotDirectory.GetDirectoryPath(release.Id)).ShouldBeFalse();
        imageHosterMock.Verify(
            hoster =>
                hoster.UploadImageAsync(
                    It.Is<ImageToUploadDto>(image =>
                        image.Source == screenshotFilePath
                        && image.SourceType == ImageUploadSource.LocalFile
                        && image.Name == "Bodies.2023.S01E03-GRP screenshot 01"
                    ),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        imageHosterMock.Verify(
            hoster =>
                hoster.UploadImageAsync(
                    It.Is<ImageToUploadDto>(image =>
                        image.Source == screenshotGridFilePath
                        && image.SourceType == ImageUploadSource.LocalFile
                        && image.Name == "Bodies.2023.S01E03-GRP screenshot grid"
                    ),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_ScreenshotFileMissing_MarksFailedWithoutUploading()
    {
        // Arrange
        var release = await AddReleaseWithScreenshotImageConfigAsync(writeScreenshotFiles: false);
        var screenshotFilePath = screenshotDirectory.GetFilePath(
            ReleaseScreenshotDirectory.GetRelativeFilePath(release.Id, "screenshot-01.jpg")
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var imageUploads = await DbContext.ImageUploads.ToListAsync();
        imageUploads.Count.ShouldBe(2);
        imageUploads.ShouldAllBe(upload => upload.UploadState == UploadState.Failed);
        imageUploads.ShouldContain(upload =>
            upload.ErrorMessages.Single()
            == $"Screenshot file '{screenshotFilePath}' does not exist. Regenerate the screenshots to upload it."
        );
        imageHosterMock.Verify(
            hoster =>
                hoster.UploadImageAsync(
                    It.IsAny<ImageToUploadDto>(),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_PendingUploadOfDeletedScreenshotFile_MarksFailedWithRegenerateMessage()
    {
        // Arrange
        var release = await AddReleaseWithScreenshotImageConfigAsync(writeScreenshotFiles: false);
        var screenshot = await DbContext.ReleaseScreenshots.FirstAsync(screenshot =>
            screenshot.ReleaseId == release.Id
        );
        screenshot.FileDeletedAt = DateTime.UtcNow;
        var imageUpload = new ImageUpload
        {
            ImageUploadConfigId = await DbContext
                .ImageUploadConfigs.Select(config => config.Id)
                .SingleAsync(),
            ReleaseScreenshotId = screenshot.Id,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Pending,
            ImageUrls = [],
            ErrorMessages = [],
        };
        DbContext.ImageUploads.Add(imageUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var persisted = await DbContext.ImageUploads.SingleAsync(upload =>
            upload.Id == imageUpload.Id
        );
        persisted.UploadState.ShouldBe(UploadState.Failed);
        persisted.ErrorMessages.ShouldBe([
            "Screenshot file was deleted after its uploads completed. Regenerate the screenshots to upload it.",
        ]);
    }

    private async Task<Release> AddReleaseWithScreenshotImageConfigAsync(bool writeScreenshotFiles)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var release = new Release
        {
            Name = "Bodies.2023.S01E03-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/bodies-screenshots",
            ReleaseGroup = releaseGroup,
            CreateScreenshots = true,
            CreateScreenshotGrid = true,
            ScreenshotsGeneratedAt = DateTime.UtcNow,
            ImageUploadConfigs = [],
        };

        var imageHosterRegistration = NewImageHosterRegistration();
        var imageUploadConfig = new ImageUploadConfig
        {
            Release = release,
            Name = "ImgBB Screens",
            ImageHosterRegistration = imageHosterRegistration,
            UploadCover = false,
            UploadScreenshots = true,
            UploadScreenshotGrid = true,
            ImageUploads = [],
        };

        DbContext.AddRange(releaseGroup, release, imageHosterRegistration, imageUploadConfig);
        await DbContext.SaveChangesAsync();

        var screenshotFileNames = new Dictionary<ReleaseScreenshotKind, string>
        {
            [ReleaseScreenshotKind.Screenshot] = "screenshot-01.jpg",
            [ReleaseScreenshotKind.ScreenshotGrid] = "screenshot-grid.jpg",
        };

        foreach (var (kind, fileName) in screenshotFileNames)
        {
            var relativeFilePath = ReleaseScreenshotDirectory.GetRelativeFilePath(
                release.Id,
                fileName
            );

            release.Screenshots.Add(
                new ReleaseScreenshot
                {
                    Kind = kind,
                    Index = 1,
                    RelativeFilePath = relativeFilePath,
                    Width = 1920,
                    Height = 1080,
                    SizeBytes = 3,
                    CreatedAt = DateTime.UtcNow,
                }
            );

            if (writeScreenshotFiles)
            {
                var filePath = screenshotDirectory.GetFilePath(relativeFilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await File.WriteAllBytesAsync(filePath, [1, 2, 3]);
            }
        }

        await DbContext.SaveChangesAsync();

        return release;
    }

    private async Task<ImageUpload> AddPendingUploadForReleaseWithoutCoverAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var release = new Release
        {
            Name = "Bodies.2023.S01E02-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/bodies-no-cover",
            ReleaseGroup = releaseGroup,
            ImageUploadConfigs = [],
        };

        var imageHosterRegistration = NewImageHosterRegistration();
        var imageUpload = new ImageUpload
        {
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Pending,
            ImageUrls = [],
            ErrorMessages = [],
        };
        var imageUploadConfig = new ImageUploadConfig
        {
            Release = release,
            Name = "ImgBB Cover",
            ImageHosterRegistration = imageHosterRegistration,
            ImageUploads = [imageUpload],
        };

        DbContext.AddRange(releaseGroup, release, imageHosterRegistration, imageUploadConfig);
        await DbContext.SaveChangesAsync();

        return imageUpload;
    }

    private void SetupSuccessfulUpload()
    {
        imageHosterMock
            .Setup(hoster =>
                hoster.UploadImageAsync(
                    It.IsAny<ImageToUploadDto>(),
                    It.IsAny<IImageHosterConfig>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ImageToUploadDto image, IImageHosterConfig _, CancellationToken _) =>
                    new UploadImageResult(
                        IsSuccess: true,
                        Image: image,
                        ImageUrls: [new ImageUrl(ImageSize.Full, "https://img.example/full.jpg")],
                        ErrorMessages: []
                    )
            );
    }

    private async Task<ReleaseCollection> AddCollectionWithImageConfigAsync(string? coverUrl)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = $"key-{Guid.NewGuid():N}",
            Name = "Bodies.2023.S01",
            CreatedAt = DateTime.UtcNow,
            Metadata = new ReleaseCollectionMetadata
            {
                MetadataDatabaseClassName = "TvdbMetadataDatabase",
                Title = "Bodies",
                CoverUrl = coverUrl,
            },
        };

        var imageHosterRegistration = NewImageHosterRegistration();
        var imageUploadConfig = new ImageUploadConfig
        {
            ReleaseCollection = collection,
            Name = "ImgBB Cover",
            ImageHosterRegistration = imageHosterRegistration,
            ImageUploads = [],
        };

        DbContext.AddRange(releaseGroup, collection, imageHosterRegistration, imageUploadConfig);
        await DbContext.SaveChangesAsync();

        return collection;
    }

    private async Task<Release> AddReleaseWithImageConfigAsync(string coverUrl)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var release = new Release
        {
            Name = "Bodies.2023.S01E01-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/bodies",
            ReleaseGroup = releaseGroup,
            ReleaseInfo = new ReleaseInfo
            {
                NfoDatabaseClassName = "ImdbNfoDatabase",
                ReleaseName = "Bodies.2023.S01E01-GRP",
            },
            Metadata = new ReleaseMetadata
            {
                MetadataDatabaseClassName = "ImdbNfoDatabase",
                Title = "Bodies",
                CoverUrl = coverUrl,
            },
            ImageUploadConfigs = [],
        };

        var imageHosterRegistration = NewImageHosterRegistration();
        var imageUploadConfig = new ImageUploadConfig
        {
            Release = release,
            Name = "ImgBB Cover",
            ImageHosterRegistration = imageHosterRegistration,
            ImageUploads = [],
        };

        DbContext.AddRange(releaseGroup, release, imageHosterRegistration, imageUploadConfig);
        await DbContext.SaveChangesAsync();

        return release;
    }

    private static ImageHosterRegistration NewImageHosterRegistration()
    {
        return new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = ImageHosterClassName,
            SerializedConfig = "{}",
            IsActive = true,
        };
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
