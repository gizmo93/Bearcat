using Bearcat.Domain.UseCases.ManageReleaseTemplates.CollectionImageUploadConfigTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates.CollectionImageUploadConfigTemplates;

public class CollectionImageUploadConfigTemplateServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private CollectionImageUploadConfigTemplateService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new CollectionImageUploadConfigTemplateService(CreateRepository());
    }

    [Test]
    public async Task CreateAsync_DetectionDisabled_Throws()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CreateAsync(
                releaseTemplate.Id,
                "Series cover",
                imageHosterRegistration.Id,
                CancellationToken.None
            )
        );

        var hasTemplate = await DbContext.CollectionImageUploadConfigTemplates.AnyAsync();
        hasTemplate.ShouldBeFalse();
    }

    [Test]
    public async Task CreateAsync_DetectionEnabled_PersistsTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        releaseTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        await DbContext.SaveChangesAsync();
        var imageHosterRegistration = await AddImageHosterRegistrationAsync();

        // Act
        await service.CreateAsync(
            releaseTemplate.Id,
            "Series cover",
            imageHosterRegistration.Id,
            CancellationToken.None
        );

        // Assert
        var template = await DbContext.CollectionImageUploadConfigTemplates.SingleAsync();
        template.ReleaseTemplateId.ShouldBe(releaseTemplate.Id);
        template.ImageHosterRegistrationId.ShouldBe(imageHosterRegistration.Id);
        template.Name.ShouldBe("Series cover");
    }

    [Test]
    public async Task UpdateAsync_TemplateExists_UpdatesNameAndImageHoster()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        releaseTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        await DbContext.SaveChangesAsync();
        var imgbb = await AddImageHosterRegistrationAsync();
        var pixhost = await AddImageHosterRegistrationAsync("PiXhost");
        var collectionImageUploadConfigTemplateId = await service.CreateAsync(
            releaseTemplate.Id,
            "Series cover",
            imgbb.Id,
            CancellationToken.None
        );
        DbContext.ChangeTracker.Clear();

        // Act
        await service.UpdateAsync(
            collectionImageUploadConfigTemplateId,
            " ",
            pixhost.Id,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var template = await DbContext.CollectionImageUploadConfigTemplates.SingleAsync();
        template.Id.ShouldBe(collectionImageUploadConfigTemplateId);
        template.Name.ShouldBeNull();
        template.ImageHosterRegistrationId.ShouldBe(pixhost.Id);
    }

    [Test]
    public async Task DeleteAsync_TemplateExists_RemovesOnlyThisTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        releaseTemplate.ReleaseCollectionDetectionMode =
            ReleaseCollectionDetectionMode.SeriesEpisodePattern;
        await DbContext.SaveChangesAsync();
        var imgbb = await AddImageHosterRegistrationAsync();
        var seriesCoverTemplateId = await service.CreateAsync(
            releaseTemplate.Id,
            "Series cover",
            imgbb.Id,
            CancellationToken.None
        );
        var seasonCoverTemplateId = await service.CreateAsync(
            releaseTemplate.Id,
            "Season cover",
            imgbb.Id,
            CancellationToken.None
        );
        DbContext.ChangeTracker.Clear();

        // Act
        await service.DeleteAsync(seriesCoverTemplateId, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var remainingTemplate = await DbContext.CollectionImageUploadConfigTemplates.SingleAsync();
        remainingTemplate.Id.ShouldBe(seasonCoverTemplateId);
        (await DbContext.ImageHosterRegistrations.SingleAsync()).Id.ShouldBe(imgbb.Id);
    }
}
