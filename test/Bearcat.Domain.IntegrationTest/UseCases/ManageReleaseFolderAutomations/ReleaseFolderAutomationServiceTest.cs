using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseFolderAutomations;

public class ReleaseFolderAutomationServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseFolderAutomationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new ReleaseFolderAutomationService(
            new ReleaseFolderAutomationRepository(dbContext, dbContext)
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidAutomation_PersistsAutomation()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();

        // Act
        var result = await service.CreateAsync(
            "  /tmp/releases  ",
            " ",
            releaseTemplate.Id,
            true,
            CancellationToken.None
        );

        // Assert
        var automation = await dbContext.ReleaseFolderAutomations.SingleAsync();

        result.ShouldBeGreaterThan(0);
        automation.Id.ShouldBe(result);
        automation.BasePath.ShouldBe("/tmp/releases");
        automation.FolderNamePattern.ShouldBeNull();
        automation.ReleaseTemplateId.ShouldBe(releaseTemplate.Id);
        automation.IsEnabled.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_AutomationExists_UpdatesAutomation()
    {
        // Arrange
        var firstTemplate = await AddReleaseTemplateAsync("First template");
        var secondTemplate = await AddReleaseTemplateAsync("Second template");
        var automation = await AddAutomationAsync(firstTemplate.Id);

        // Act
        await service.UpdateAsync(
            automation.Id,
            "/tmp/updated",
            "*1080p*",
            secondTemplate.Id,
            false,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.ReleaseFolderAutomations.SingleAsync();

        result.BasePath.ShouldBe("/tmp/updated");
        result.FolderNamePattern.ShouldBe("*1080p*");
        result.ReleaseTemplateId.ShouldBe(secondTemplate.Id);
        result.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task SetEnabledAsync_AutomationExists_TogglesEnabledState()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var automation = await AddAutomationAsync(releaseTemplate.Id, isEnabled: true);

        // Act
        await service.SetEnabledAsync(automation.Id, false, CancellationToken.None);

        // Assert
        var result = await dbContext.ReleaseFolderAutomations.SingleAsync();

        result.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_AutomationExists_RemovesAutomation()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var automation = await AddAutomationAsync(releaseTemplate.Id);

        // Act
        await service.DeleteAsync(automation.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.ReleaseFolderAutomations.AnyAsync();

        result.ShouldBeFalse();
    }

    private async Task<ReleaseTemplate> AddReleaseTemplateAsync(string name = "Managed template")
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"{name} group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var releaseTemplate = new ReleaseTemplate
        {
            Name = name,
            ReleaseType = ReleaseType.Managed,
            ReleaseGroup = releaseGroup,
        };

        dbContext.ReleaseTemplates.Add(releaseTemplate);
        await dbContext.SaveChangesAsync();

        return releaseTemplate;
    }

    private async Task<ReleaseFolderAutomation> AddAutomationAsync(
        int releaseTemplateId,
        bool isEnabled = true
    )
    {
        var automation = new ReleaseFolderAutomation
        {
            BasePath = "/tmp/releases",
            FolderNamePattern = null,
            ReleaseTemplateId = releaseTemplateId,
            IsEnabled = isEnabled,
        };

        dbContext.ReleaseFolderAutomations.Add(automation);
        await dbContext.SaveChangesAsync();

        return automation;
    }
}
