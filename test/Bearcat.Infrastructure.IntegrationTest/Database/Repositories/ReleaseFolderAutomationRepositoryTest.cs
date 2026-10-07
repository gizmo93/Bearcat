using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ReleaseFolderAutomationRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime LastChangedAt = new(
        2026,
        10,
        7,
        10,
        0,
        0,
        DateTimeKind.Unspecified
    );

    [Test]
    public async Task GetAllAsync_AutomationWithExtractionEnabled_ProjectsExtractionFlag()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var releaseTemplate = AddReleaseTemplate(dbContext);
        var extractingAutomation = AddAutomation(
            dbContext,
            releaseTemplate,
            "/data/extracting",
            extractArchivesBeforeReleaseCreation: true
        );
        var plainAutomation = AddAutomation(
            dbContext,
            releaseTemplate,
            "/data/plain",
            extractArchivesBeforeReleaseCreation: false
        );
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();

        // Act
        var result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result
            .Select(automation =>
                (
                    automation.ReleaseFolderAutomationId,
                    automation.ExtractArchivesBeforeReleaseCreation
                )
            )
            .ShouldBe([(extractingAutomation.Id, true), (plainAutomation.Id, false)]);
    }

    [Test]
    public async Task GetFailedExtractionsAsync_MixedObservations_ReturnsOnlyFailedOnesNewestFirst()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var olderFailure = AddObservation(
            dbContext,
            "/data/Older.Failure",
            extractionErrorMessage: "Older error",
            extractionFailedAt: LastChangedAt.AddHours(1)
        );
        AddObservation(
            dbContext,
            "/data/Waiting.Folder",
            extractionErrorMessage: null,
            extractionFailedAt: null
        );
        var newerFailure = AddObservation(
            dbContext,
            "/data/Newer.Failure",
            extractionErrorMessage: "Newer error",
            extractionFailedAt: LastChangedAt.AddHours(2)
        );
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();

        // Act
        var result = await repository.GetFailedExtractionsAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new FailedReleaseFolderExtractionReadModel(
                ReleaseFolderObservationId: newerFailure.Id,
                FolderPath: "/data/Newer.Failure",
                ErrorMessage: "Newer error",
                FailedAt: LastChangedAt.AddHours(2)
            ),
            new FailedReleaseFolderExtractionReadModel(
                ReleaseFolderObservationId: olderFailure.Id,
                FolderPath: "/data/Older.Failure",
                ErrorMessage: "Older error",
                FailedAt: LastChangedAt.AddHours(1)
            ),
        ]);
    }

    [Test]
    public async Task GetFailedExtractionsAsync_NoFailedObservations_ReturnsEmptyList()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        AddObservation(
            dbContext,
            "/data/Waiting.Folder",
            extractionErrorMessage: null,
            extractionFailedAt: null
        );
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();

        // Act
        var result = await repository.GetFailedExtractionsAsync(CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetObservationByIdAsync_ObservationExists_ReturnsTrackedObservationWhoseChangesAreSaved()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        AddObservation(
            dbContext,
            "/data/Other.Folder",
            extractionErrorMessage: null,
            extractionFailedAt: null
        );
        var failedObservation = AddObservation(
            dbContext,
            "/data/Failed.Folder",
            extractionErrorMessage: "CRC failed",
            extractionFailedAt: LastChangedAt.AddHours(1)
        );
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();

        // Act
        var result = await repository.GetObservationByIdAsync(
            failedObservation.Id,
            CancellationToken.None
        );
        result.ExtractionErrorMessage = null;
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        result.FolderPath.ShouldBe("/data/Failed.Folder");
        result.FileCount.ShouldBe(2);
        result.TotalBytes.ShouldBe(200);
        result.LastChangedAt.ShouldBe(LastChangedAt);
        result.ExtractionFailedAt.ShouldBe(LastChangedAt.AddHours(1));
        await using var verificationDbContext = CreateDbContext();
        var savedObservation = await verificationDbContext.ReleaseFolderObservations.SingleAsync(
            observation => observation.Id == failedObservation.Id
        );
        savedObservation.ExtractionErrorMessage.ShouldBeNull();
    }

    private ReleaseFolderAutomationRepository CreateRepository()
    {
        var dbContext = CreateDbContext();

        return new ReleaseFolderAutomationRepository(dbContext, dbContext);
    }

    private static ReleaseTemplate AddReleaseTemplate(BearcatDbContext dbContext)
    {
        var releaseTemplate = new ReleaseTemplate
        {
            Name = "Managed template",
            ReleaseType = ReleaseType.Managed,
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Managed releases",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        dbContext.ReleaseTemplates.Add(releaseTemplate);

        return releaseTemplate;
    }

    private static ReleaseFolderAutomation AddAutomation(
        BearcatDbContext dbContext,
        ReleaseTemplate releaseTemplate,
        string basePath,
        bool extractArchivesBeforeReleaseCreation
    )
    {
        var automation = new ReleaseFolderAutomation
        {
            BasePath = basePath,
            ReleaseTemplate = releaseTemplate,
            ExtractArchivesBeforeReleaseCreation = extractArchivesBeforeReleaseCreation,
            IsEnabled = true,
        };
        dbContext.ReleaseFolderAutomations.Add(automation);

        return automation;
    }

    private static ReleaseFolderObservation AddObservation(
        BearcatDbContext dbContext,
        string folderPath,
        string? extractionErrorMessage,
        DateTime? extractionFailedAt
    )
    {
        var observation = new ReleaseFolderObservation
        {
            FolderPath = folderPath,
            FileCount = 2,
            TotalBytes = 200,
            LastChangedAt = LastChangedAt,
            ExtractionErrorMessage = extractionErrorMessage,
            ExtractionFailedAt = extractionFailedAt,
        };
        dbContext.ReleaseFolderObservations.Add(observation);

        return observation;
    }
}
