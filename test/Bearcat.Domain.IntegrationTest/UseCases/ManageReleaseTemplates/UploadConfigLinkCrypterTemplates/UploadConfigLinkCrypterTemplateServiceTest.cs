using Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigLinkCrypterTemplates;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseTemplates.UploadConfigLinkCrypterTemplates;

public class UploadConfigLinkCrypterTemplateServiceTest(DatabaseProvider databaseProvider)
    : ReleaseTemplateIntegrationTest(databaseProvider)
{
    private UploadConfigLinkCrypterTemplateService service = null!;

    [SetUp]
    public void Setup()
    {
        service = new UploadConfigLinkCrypterTemplateService(CreateRepository());
    }

    [Test]
    public async Task UpdateAsync_TemplateExists_UpdatesLinkCrypterPassword()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.UpdateAsync(
            seed.UploadConfigLinkCrypterTemplateId,
            " ",
            true,
            true,
            true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var result = await DbContext.UploadConfigLinkCrypterTemplates.SingleAsync();

        result.Password.ShouldBeNull();
        result.EnableCaptcha.ShouldBeTrue();
        result.EnableContainerDownload.ShouldBeTrue();
        result.EnableClickAndLoad.ShouldBeTrue();
    }

    [Test]
    public async Task DeleteAsync_TemplateExists_RemovesLinkCrypterTemplate()
    {
        // Arrange
        var seed = await AddReleaseTemplateWithChildrenAsync();

        // Act
        await service.DeleteAsync(seed.UploadConfigLinkCrypterTemplateId, CancellationToken.None);

        // Assert
        (await DbContext.UploadConfigLinkCrypterTemplates.AnyAsync()).ShouldBeFalse();
        (
            await DbContext.UploadConfigTemplates.AnyAsync(u => u.Id == seed.UploadConfigTemplateId)
        ).ShouldBeTrue();
    }
}
