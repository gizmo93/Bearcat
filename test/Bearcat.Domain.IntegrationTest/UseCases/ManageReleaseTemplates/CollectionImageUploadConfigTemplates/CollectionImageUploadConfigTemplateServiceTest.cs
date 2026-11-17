using Bearcat.Domain.UseCases.ManageReleaseTemplates.CollectionImageUploadConfigTemplates;
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
}
