using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;
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

    private ReleaseFolderAutomationRepository CreateRepository()
    {
        var dbContext = CreateDbContext();

        return new ReleaseFolderAutomationRepository(dbContext, dbContext);
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
