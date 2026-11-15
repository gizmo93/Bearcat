using Bearcat.Domain.UseCases.ManageReleaseTemplates.ImageUploadConfigTemplates;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates.ImageUploadConfigTemplates;

public class ImageUploadConfigTemplateServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private ImageUploadConfigTemplateService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new ImageUploadConfigTemplateService(CreateRepository());
    }

    [Test]
    public async Task UpdateAsync_TemplateExists_UpdatesNameImageHosterAndUploadedImages()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var imgbb = await AddImageHosterRegistrationAsync();
        var pixhost = await AddImageHosterRegistrationAsync("PiXhost");
        var imageUploadConfigTemplateId = await service.CreateAsync(
            releaseTemplateId: releaseTemplate.Id,
            name: "Cover",
            imageHosterRegistrationId: imgbb.Id,
            uploadCover: true,
            uploadScreenshots: false,
            uploadScreenshotGrid: false,
            cancellationToken: CancellationToken.None
        );
        DbContext.ChangeTracker.Clear();

        // Act
        await service.UpdateAsync(
            imageUploadConfigTemplateId: imageUploadConfigTemplateId,
            name: "  Screens  ",
            imageHosterRegistrationId: pixhost.Id,
            uploadCover: false,
            uploadScreenshots: true,
            uploadScreenshotGrid: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var template = await DbContext.ImageUploadConfigTemplates.SingleAsync();
        template.Id.ShouldBe(imageUploadConfigTemplateId);
        template.Name.ShouldBe("Screens");
        template.ImageHosterRegistrationId.ShouldBe(pixhost.Id);
        template.UploadCover.ShouldBeFalse();
        template.UploadScreenshots.ShouldBeTrue();
        template.UploadScreenshotGrid.ShouldBeTrue();
    }

    [Test]
    public async Task DeleteAsync_TemplateExists_RemovesOnlyThisTemplate()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync();
        var releaseTemplate = await AddReleaseTemplateAsync(releaseGroup.Id);
        var imgbb = await AddImageHosterRegistrationAsync();
        var coverTemplateId = await service.CreateAsync(
            releaseTemplateId: releaseTemplate.Id,
            name: "Cover",
            imageHosterRegistrationId: imgbb.Id,
            uploadCover: true,
            uploadScreenshots: false,
            uploadScreenshotGrid: false,
            cancellationToken: CancellationToken.None
        );
        var screensTemplateId = await service.CreateAsync(
            releaseTemplateId: releaseTemplate.Id,
            name: "Screens",
            imageHosterRegistrationId: imgbb.Id,
            uploadCover: true,
            uploadScreenshots: false,
            uploadScreenshotGrid: false,
            cancellationToken: CancellationToken.None
        );
        DbContext.ChangeTracker.Clear();

        // Act
        await service.DeleteAsync(coverTemplateId, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var remainingTemplate = await DbContext.ImageUploadConfigTemplates.SingleAsync();
        remainingTemplate.Id.ShouldBe(screensTemplateId);
        (await DbContext.ImageHosterRegistrations.SingleAsync()).Id.ShouldBe(imgbb.Id);
    }
}
