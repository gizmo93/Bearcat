using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs;
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

    private async Task<ImageHosterRegistration> AddImageHosterRegistrationAsync()
    {
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = "ImgBB",
            ImageHosterClassName = "ImgBb",
            SerializedConfig = "{}",
            IsActive = true,
        };

        dbContext.ImageHosterRegistrations.Add(imageHosterRegistration);
        await dbContext.SaveChangesAsync();

        return imageHosterRegistration;
    }
}
