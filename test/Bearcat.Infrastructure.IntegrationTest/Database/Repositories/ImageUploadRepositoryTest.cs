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
    public async Task CreateMissingImageUploadsAsync_CollectionWithoutCover_DoesNotCreateUpload()
    {
        // Arrange
        var config = await AddCollectionImageUploadConfigAsync(coverUrl: null);

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
    public async Task CreateMissingImageUploadsAsync_CalledTwice_CreatesOnlyOneUpload()
    {
        // Arrange
        var config = await AddReleaseImageUploadConfigAsync(coverUrl: "https://cover.test/1.jpg");

        // Act
        await repository.CreateMissingImageUploadsAsync(CreatedAt, CancellationToken.None);
        await repository.CreateMissingImageUploadsAsync(
            CreatedAt.AddMinutes(1),
            CancellationToken.None
        );

        // Assert
        var upload = (await GetImageUploadsAsync(config)).ShouldHaveSingleItem();
        upload.CreatedAt.ShouldBe(CreatedAt);
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
        var release = new Release
        {
            Name = "Bodies.2023.S01E01-GRP",
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
