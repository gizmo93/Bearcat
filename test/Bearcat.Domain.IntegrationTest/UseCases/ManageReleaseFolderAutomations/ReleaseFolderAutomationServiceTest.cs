using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseFolderAutomations;

public class ReleaseFolderAutomationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ReleaseFolderAutomationRepository repository = null!;
    private ReleaseFolderAutomationService service = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ReleaseFolderAutomationRepository(DbContext, DbContext);
        service = new ReleaseFolderAutomationService(repository);
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
            "de",
            true,
            true,
            true,
            CancellationToken.None
        );

        // Assert
        var automation = await DbContext.ReleaseFolderAutomations.SingleAsync();

        result.ShouldBeGreaterThan(0);
        automation.Id.ShouldBe(result);
        automation.BasePath.ShouldBe("/tmp/releases");
        automation.FolderNamePattern.ShouldBeNull();
        automation.ReleaseTemplateId.ShouldBe(releaseTemplate.Id);
        automation.PrimaryLanguageCode.ShouldBe("de");
        automation.ExtractArchivesBeforeReleaseCreation.ShouldBeTrue();
        automation.IsEnabled.ShouldBeTrue();
        automation.IgnoreExistingOnFirstScan.ShouldBeTrue();
        automation.HasCompletedInitialScan.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_AutomationExists_UpdatesAutomationAndRestartsInitialScan()
    {
        // Arrange
        var firstTemplate = await AddReleaseTemplateAsync("First template");
        var secondTemplate = await AddReleaseTemplateAsync("Second template");
        var automation = await AddAutomationAsync(firstTemplate.Id, hasCompletedInitialScan: true);

        // Act
        await service.UpdateAsync(
            automation.Id,
            "/tmp/updated",
            "*1080p*",
            secondTemplate.Id,
            "en",
            true,
            false,
            true,
            CancellationToken.None
        );

        // Assert
        var result = await DbContext.ReleaseFolderAutomations.SingleAsync();

        result.BasePath.ShouldBe("/tmp/updated");
        result.FolderNamePattern.ShouldBe("*1080p*");
        result.ReleaseTemplateId.ShouldBe(secondTemplate.Id);
        result.PrimaryLanguageCode.ShouldBe("en");
        result.ExtractArchivesBeforeReleaseCreation.ShouldBeTrue();
        result.IsEnabled.ShouldBeFalse();
        result.IgnoreExistingOnFirstScan.ShouldBeTrue();
        result.HasCompletedInitialScan.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_BasePathAndPatternUnchanged_KeepsCompletedInitialScan()
    {
        // Arrange
        var releaseTemplate = await AddReleaseTemplateAsync();
        var automation = await AddAutomationAsync(
            releaseTemplate.Id,
            hasCompletedInitialScan: true
        );

        // Act
        await service.UpdateAsync(
            automation.Id,
            " /tmp/releases ",
            " ",
            releaseTemplate.Id,
            "en",
            false,
            true,
            true,
            CancellationToken.None
        );

        // Assert
        var result = await CreateDbContext()
            .ReleaseFolderAutomations.SingleAsync(a => a.Id == automation.Id);

        result.HasCompletedInitialScan.ShouldBeTrue();
        result.IgnoreExistingOnFirstScan.ShouldBeTrue();
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
        var result = await DbContext.ReleaseFolderAutomations.SingleAsync();

        result.IsEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task RetryExtractionAsync_ExtractionFailed_ClearsErrorAndKeepsMeasuredFolderState()
    {
        // Arrange
        var failedAt = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Unspecified);
        var lastChangedAt = new DateTime(2026, 10, 7, 11, 0, 0, DateTimeKind.Unspecified);
        var observation = new ReleaseFolderObservation
        {
            FolderPath = "/tmp/releases/Broken.Release",
            FileCount = 3,
            TotalBytes = 300,
            LastChangedAt = lastChangedAt,
            ExtractionErrorMessage =
                "CRC failed. The files were kept in /tmp/releases/Broken.Release.",
            ExtractionFailedAt = failedAt,
        };
        DbContext.ReleaseFolderObservations.Add(observation);
        await DbContext.SaveChangesAsync();

        // Act
        await service.RetryExtractionAsync(observation.Id, CancellationToken.None);

        // Assert
        var result = await CreateDbContext()
            .ReleaseFolderObservations.SingleAsync(o => o.Id == observation.Id);

        result.ExtractionErrorMessage.ShouldBeNull();
        result.ExtractionFailedAt.ShouldBeNull();
        result.FileCount.ShouldBe(3);
        result.TotalBytes.ShouldBe(300);
        result.LastChangedAt.ShouldBe(lastChangedAt);
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
        var result = await DbContext.ReleaseFolderAutomations.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetAllAsync_AutomationsWithTemplates_ReturnsReadModelsOrderedByBasePathAndPattern()
    {
        // Arrange
        var movieTemplate = await AddReleaseTemplateAsync("Movie template");
        movieTemplate.ReleaseContentType = ReleaseContentType.Movie;
        var seriesTemplate = await AddReleaseTemplateAsync("Series template");
        seriesTemplate.ReleaseContentType = ReleaseContentType.TvShowEpisode;
        seriesTemplate.ReleaseType = ReleaseType.Unmanaged;
        await DbContext.SaveChangesAsync();
        var otherBasePathAutomation = await AddAutomationAsync(movieTemplate.Id);
        otherBasePathAutomation.BasePath = "/data/movies";
        var lowResolutionAutomation = await AddAutomationAsync(seriesTemplate.Id, isEnabled: false);
        lowResolutionAutomation.BasePath = "/data/incoming";
        lowResolutionAutomation.FolderNamePattern = "*720p*";
        var highResolutionAutomation = await AddAutomationAsync(movieTemplate.Id);
        highResolutionAutomation.BasePath = "/data/incoming";
        highResolutionAutomation.FolderNamePattern = "*1080p*";
        highResolutionAutomation.PrimaryLanguageCode = "de";
        highResolutionAutomation.ExtractArchivesBeforeReleaseCreation = true;
        highResolutionAutomation.IgnoreExistingOnFirstScan = true;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new ReleaseFolderAutomationReadModel(
                ReleaseFolderAutomationId: highResolutionAutomation.Id,
                BasePath: "/data/incoming",
                FolderNamePattern: "*1080p*",
                ReleaseTemplateId: movieTemplate.Id,
                ReleaseTemplateName: "Movie template",
                ReleaseType: ReleaseType.Managed,
                ReleaseContentType: ReleaseContentType.Movie,
                PrimaryLanguageCode: "de",
                ExtractArchivesBeforeReleaseCreation: true,
                IsEnabled: true,
                IgnoreExistingOnFirstScan: true
            ),
            new ReleaseFolderAutomationReadModel(
                ReleaseFolderAutomationId: lowResolutionAutomation.Id,
                BasePath: "/data/incoming",
                FolderNamePattern: "*720p*",
                ReleaseTemplateId: seriesTemplate.Id,
                ReleaseTemplateName: "Series template",
                ReleaseType: ReleaseType.Unmanaged,
                ReleaseContentType: ReleaseContentType.TvShowEpisode,
                PrimaryLanguageCode: null,
                ExtractArchivesBeforeReleaseCreation: false,
                IsEnabled: false,
                IgnoreExistingOnFirstScan: false
            ),
            new ReleaseFolderAutomationReadModel(
                ReleaseFolderAutomationId: otherBasePathAutomation.Id,
                BasePath: "/data/movies",
                FolderNamePattern: null,
                ReleaseTemplateId: movieTemplate.Id,
                ReleaseTemplateName: "Movie template",
                ReleaseType: ReleaseType.Managed,
                ReleaseContentType: ReleaseContentType.Movie,
                PrimaryLanguageCode: null,
                ExtractArchivesBeforeReleaseCreation: false,
                IsEnabled: true,
                IgnoreExistingOnFirstScan: false
            ),
        ]);
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

        DbContext.ReleaseTemplates.Add(releaseTemplate);
        await DbContext.SaveChangesAsync();

        return releaseTemplate;
    }

    private async Task<ReleaseFolderAutomation> AddAutomationAsync(
        int releaseTemplateId,
        bool isEnabled = true,
        bool hasCompletedInitialScan = false
    )
    {
        var automation = new ReleaseFolderAutomation
        {
            BasePath = "/tmp/releases",
            FolderNamePattern = null,
            ReleaseTemplateId = releaseTemplateId,
            IsEnabled = isEnabled,
            HasCompletedInitialScan = hasCompletedInitialScan,
        };

        DbContext.ReleaseFolderAutomations.Add(automation);
        await DbContext.SaveChangesAsync();

        return automation;
    }
}
