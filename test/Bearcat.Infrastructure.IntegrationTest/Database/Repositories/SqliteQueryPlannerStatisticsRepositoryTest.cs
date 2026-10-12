using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class SqliteQueryPlannerStatisticsRepositoryTest
{
    private const int NotificationCount = 10;

    private static readonly DateTime CreatedAt = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

    private SqliteIntegrationTestDatabase database = null!;
    private BearcatDbContext dbContext = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        database = await SqliteIntegrationTestDatabaseTemplate.CreateTestDatabaseAsync();
        dbContext = database.CreateDbContext();
    }

    [TearDown]
    public async Task DisposeDatabaseAsync()
    {
        await dbContext.DisposeAsync();
        await database.DisposeAsync();
    }

    [Test]
    public async Task UpdateQueryPlannerStatisticsAsync_TableWithRowsNeverAnalyzed_WritesStatisticsForTheTable()
    {
        // Arrange
        dbContext.Notifications.AddRange(
            Enumerable
                .Range(0, NotificationCount)
                .Select(index => new Notification
                {
                    CreatedAt = CreatedAt.AddMinutes(index),
                    NotificationSeverity = NotificationSeverity.Info,
                    NotificationKind = NotificationKind.Legacy,
                    Message = $"Notification {index}",
                })
        );
        await dbContext.SaveChangesAsync();
        await using var repositoryDbContext = database.CreateDbContext();
        var repository = new SqliteQueryPlannerStatisticsRepository(repositoryDbContext);

        // Act
        await repository.UpdateQueryPlannerStatisticsAsync(CancellationToken.None);

        // Assert
        var statisticsRowCount = await dbContext
            .Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS \"Value\" FROM sqlite_stat1 WHERE tbl = 'Notifications'"
            )
            .SingleAsync();
        statisticsRowCount.ShouldBeGreaterThan(0);
    }
}
