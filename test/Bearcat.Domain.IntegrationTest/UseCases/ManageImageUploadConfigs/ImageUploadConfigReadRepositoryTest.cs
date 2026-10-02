using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageImageUploadConfigs;

public class ImageUploadConfigReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ImageUploadConfigReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ImageUploadConfigReadRepository(DbContext);
    }

    [Test]
    public async Task GetImageUploadConfigsAsync_ReleaseAndCollectionConfigs_ReturnsConfigsOfReleaseWithUploadCounts()
    {
        // Arrange
        var seed = await AddImageUploadConfigsAsync();

        // Act
        var result = await repository.GetImageUploadConfigsAsync(
            seed.ReleaseId,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new ImageUploadConfigReadModel(
                seed.CoverConfigId,
                "Cover",
                seed.ImgBbRegistrationId,
                "ImgBB",
                "Bearcat.Release.2026-GRP",
                2
            ),
            new ImageUploadConfigReadModel(
                seed.ScreensConfigId,
                "Screens",
                seed.PixHostRegistrationId,
                "PixHost",
                "Bearcat.Release.2026-GRP",
                0
            ),
        ]);
    }

    [Test]
    public async Task GetReadModelByIdAsync_ConfigWithUploads_ReturnsReadModel()
    {
        // Arrange
        var seed = await AddImageUploadConfigsAsync();

        // Act
        var result = await repository.GetReadModelByIdAsync(
            seed.CoverConfigId,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new ImageUploadConfigReadModel(
                seed.CoverConfigId,
                "Cover",
                seed.ImgBbRegistrationId,
                "ImgBB",
                "Bearcat.Release.2026-GRP",
                2
            )
        );
    }

    [Test]
    public async Task GetImageHosterRegistrationOptionsAsync_ActiveAndInactiveRegistrations_ReturnsActiveRegistrations()
    {
        // Arrange
        var seed = await AddImageUploadConfigsAsync();

        // Act
        var result = await repository.GetImageHosterRegistrationOptionsAsync(
            CancellationToken.None
        );

        // Assert
        result.Count.ShouldBe(2);
        result[seed.ImgBbRegistrationId].ShouldBe("ImgBB");
        result[seed.PixHostRegistrationId].ShouldBe("PixHost");
        result.ShouldNotContainKey(seed.InactiveRegistrationId);
    }

    private async Task<ImageUploadConfigSeed> AddImageUploadConfigsAsync()
    {
        var imgBb = CreateImageHosterRegistration("ImgBB", isActive: true);
        var pixHost = CreateImageHosterRegistration("PixHost", isActive: true);
        var inactive = CreateImageHosterRegistration("Inactive", isActive: false);
        var releaseGroup = new ReleaseGroup
        {
            Name = "Release group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = CreateRelease("Bearcat.Release.2026-GRP", releaseGroup);
        var otherRelease = CreateRelease("Bearcat.Other.2026-GRP", releaseGroup);
        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = "bearcat-collection",
            Name = "Bearcat.Collection",
            CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
        };
        var coverConfig = new ImageUploadConfig
        {
            Release = release,
            ImageHosterRegistration = imgBb,
            Name = "Cover",
            ImageUploads = [CreateImageUpload(), CreateImageUpload()],
        };
        var screensConfig = new ImageUploadConfig
        {
            Release = release,
            ImageHosterRegistration = pixHost,
            Name = "Screens",
        };
        var otherReleaseConfig = new ImageUploadConfig
        {
            Release = otherRelease,
            ImageHosterRegistration = imgBb,
            Name = "Other cover",
            ImageUploads = [CreateImageUpload()],
        };
        var collectionConfig = new ImageUploadConfig
        {
            ReleaseCollection = collection,
            ImageHosterRegistration = inactive,
            Name = "Collection cover",
            ImageUploads = [CreateImageUpload()],
        };

        DbContext.ImageUploadConfigs.Add(coverConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ImageUploadConfigs.Add(screensConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ImageUploadConfigs.AddRange(otherReleaseConfig, collectionConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new ImageUploadConfigSeed(
            release.Id,
            coverConfig.Id,
            screensConfig.Id,
            imgBb.Id,
            pixHost.Id,
            inactive.Id
        );
    }

    private static ImageHosterRegistration CreateImageHosterRegistration(string name, bool isActive)
    {
        return new ImageHosterRegistration
        {
            Name = name,
            ImageHosterClassName = name,
            SerializedConfig = "{}",
            IsActive = isActive,
        };
    }

    private static Release CreateRelease(string name, ReleaseGroup releaseGroup)
    {
        return new Release
        {
            Name = name,
            CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = releaseGroup,
        };
    }

    private static ImageUpload CreateImageUpload()
    {
        return new ImageUpload
        {
            CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            UploadState = UploadState.Completed,
            ErrorMessages = [],
        };
    }

    private sealed record ImageUploadConfigSeed(
        int ReleaseId,
        int CoverConfigId,
        int ScreensConfigId,
        int ImgBbRegistrationId,
        int PixHostRegistrationId,
        int InactiveRegistrationId
    );
}
