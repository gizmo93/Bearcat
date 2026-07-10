using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploads;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageImageUploads;

public class ImageUploadServiceTest : BearcatIntegrationTest
{
    private const string ImageHosterClassName = "ImgBb";

    private BearcatDbContext dbContext = null!;
    private Mock<IImageHoster> imageHosterMock = null!;
    private ImageUploadService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();

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
            new ImageUploadRepository(dbContext, NoOpSecretProtector.Instance),
            imageHosterFactoryMock.Object,
            CreateTimeProvider(),
            NullLogger<ImageUploadService>.Instance
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
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
        var imageUpload = await dbContext
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
    public async Task ProcessAsync_CollectionWithoutCover_DoesNotCreateUpload()
    {
        // Arrange
        var collection = await AddCollectionWithImageConfigAsync(coverUrl: null);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var hasUpload = await dbContext.ImageUploads.AnyAsync(upload =>
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
        var imageUpload = await dbContext.ImageUploads.SingleAsync(upload =>
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
        var persisted = await dbContext.ImageUploads.SingleAsync(upload =>
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
        var imageUpload = await dbContext.ImageUploads.SingleAsync(upload =>
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
        var imageUpload = await dbContext.ImageUploads.SingleAsync(upload =>
            upload.ImageUploadConfig.ReleaseCollectionId == collection.Id
        );
        imageUpload.UploadState.ShouldBe(UploadState.Failed);
        imageUpload.ErrorMessages.ShouldBe(["inner cause"]);
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

        dbContext.AddRange(releaseGroup, release, imageHosterRegistration, imageUploadConfig);
        await dbContext.SaveChangesAsync();

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
                SeriesDatabaseClassName = "TvdbSeriesDatabase",
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

        dbContext.AddRange(releaseGroup, collection, imageHosterRegistration, imageUploadConfig);
        await dbContext.SaveChangesAsync();

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

        dbContext.AddRange(releaseGroup, release, imageHosterRegistration, imageUploadConfig);
        await dbContext.SaveChangesAsync();

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
