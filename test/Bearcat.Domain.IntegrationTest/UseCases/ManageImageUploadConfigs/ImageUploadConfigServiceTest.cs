using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageImageUploadConfigs;

public class ImageUploadConfigServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ImageUploadConfigService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new ImageUploadConfigService(new ImageUploadConfigWriteRepository(dbContext));
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateForCollectionAsync_WithoutName_UsesImageHosterName()
    {
        // Arrange
        var collection = await AddCollectionAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();

        // Act
        var configId = await service.CreateForCollectionAsync(
            collection.Id,
            name: "   ",
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Assert
        var config = await dbContext.ImageUploadConfigs.SingleAsync(c => c.Id == configId);
        config.Name.ShouldBe(imageHosterRegistration.Name);
        config.ReleaseCollectionId.ShouldBe(collection.Id);
    }

    [Test]
    public async Task CreateForCollectionAsync_WithName_UsesTrimmedName()
    {
        // Arrange
        var collection = await AddCollectionAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();

        // Act
        var configId = await service.CreateForCollectionAsync(
            collection.Id,
            name: "  Series cover  ",
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Assert
        var config = await dbContext.ImageUploadConfigs.SingleAsync(c => c.Id == configId);
        config.Name.ShouldBe("Series cover");
    }

    [Test]
    public async Task CreateAsync_WithoutName_UsesImageHosterNameAndLinksRelease()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();

        // Act
        var configId = await service.CreateAsync(
            release.Id,
            name: null,
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Assert
        var config = await dbContext.ImageUploadConfigs.SingleAsync(c => c.Id == configId);
        config.Name.ShouldBe(imageHosterRegistration.Name);
        config.ReleaseId.ShouldBe(release.Id);
        config.ImageHosterRegistrationId.ShouldBe(imageHosterRegistration.Id);
    }

    [Test]
    public async Task CreateAsync_WithName_UsesTrimmedName()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();

        // Act
        var configId = await service.CreateAsync(
            release.Id,
            name: "  Release cover  ",
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Assert
        var config = await dbContext.ImageUploadConfigs.SingleAsync(c => c.Id == configId);
        config.Name.ShouldBe("Release cover");
    }

    [Test]
    public async Task UpdateAsync_WithoutName_UsesImageHosterName()
    {
        // Arrange
        var collection = await AddCollectionAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();
        var configId = await service.CreateForCollectionAsync(
            collection.Id,
            "Series cover",
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Act
        await service.UpdateAsync(
            configId,
            name: null,
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Assert
        var config = await dbContext.ImageUploadConfigs.SingleAsync(c => c.Id == configId);
        config.Name.ShouldBe(imageHosterRegistration.Name);
    }

    [Test]
    public async Task UpdateAsync_WithName_UpdatesNameAndImageHosterRegistration()
    {
        // Arrange
        var collection = await AddCollectionAsync();
        var firstHosterRegistration = await AddImageHosterRegistrationAsync();
        var secondHosterRegistration = await AddImageHosterRegistrationAsync("Imgur", "Imgur");
        var configId = await service.CreateForCollectionAsync(
            collection.Id,
            "Series cover",
            firstHosterRegistration.Id,
            CancellationToken.None
        );

        // Act
        await service.UpdateAsync(
            configId,
            name: "  Updated cover  ",
            secondHosterRegistration.Id,
            CancellationToken.None
        );

        // Assert
        var config = await dbContext.ImageUploadConfigs.SingleAsync(c => c.Id == configId);
        config.Name.ShouldBe("Updated cover");
        config.ImageHosterRegistrationId.ShouldBe(secondHosterRegistration.Id);
    }

    [Test]
    public async Task DeleteAsync_ConfigExists_RemovesConfig()
    {
        // Arrange
        var collection = await AddCollectionAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();
        var configId = await service.CreateForCollectionAsync(
            collection.Id,
            "Series cover",
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Act
        await service.DeleteAsync(configId, CancellationToken.None);

        // Assert
        (await dbContext.ImageUploadConfigs.AnyAsync()).ShouldBeFalse();
    }

    private async Task<Release> AddReleaseAsync()
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
            ReleaseGroup = releaseGroup,
            Name = $"Bearcat.Release.{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        dbContext.AddRange(releaseGroup, release);
        await dbContext.SaveChangesAsync();

        return release;
    }

    private async Task<ReleaseCollection> AddCollectionAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = $"key-{Guid.NewGuid():N}",
            Name = "Bodies.2023.S01",
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.AddRange(releaseGroup, collection);
        await dbContext.SaveChangesAsync();

        return collection;
    }

    private async Task<ImageHosterRegistration> AddImageHosterRegistrationAsync(
        string name = "ImgBB",
        string className = "ImgBb"
    )
    {
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = name,
            ImageHosterClassName = className,
            SerializedConfig = "{}",
            IsActive = true,
        };

        dbContext.ImageHosterRegistrations.Add(imageHosterRegistration);
        await dbContext.SaveChangesAsync();

        return imageHosterRegistration;
    }
}
