using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class BackgroundTaskStateRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private BackgroundTaskStateRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var readDbContext = CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        repository = new BackgroundTaskStateRepository(readDbContext, CreateDbContext());
    }

    [Test]
    public async Task GetAllAsync_TasksWithAndWithoutOverride_ReturnsTasksOrderedByDisplayNameWithExactIntervals()
    {
        // Arrange
        var defaultIntervalAboveOneDay = new TimeSpan(
            days: 2,
            hours: 3,
            minutes: 4,
            seconds: 5,
            milliseconds: 678
        );
        var intervalOverride = TimeSpan.FromMinutes(90) + TimeSpan.FromMilliseconds(250);
        var lastStartedAt = new DateTime(2026, 9, 3, 23, 59, 59, 999, DateTimeKind.Utc);
        var lastFinishedAt = new DateTime(2026, 9, 4, 0, 0, 0, 1, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc);
        var betaTask = new BackgroundTaskState
        {
            Key = "beta-task",
            DisplayName = "Beta task",
            IsEnabled = false,
            DefaultInterval = TimeSpan.FromMinutes(15),
            IntervalOverride = null,
            UpdatedAt = updatedAt,
        };
        var alphaTask = new BackgroundTaskState
        {
            Key = "alpha-task",
            DisplayName = "Alpha task",
            IsEnabled = true,
            DefaultInterval = defaultIntervalAboveOneDay,
            IntervalOverride = intervalOverride,
            LastStartedAt = lastStartedAt,
            LastFinishedAt = lastFinishedAt,
            LastExecutionStatus = BackgroundTaskExecutionStatus.Error,
            LastErrorMessage = "Résumé import failed",
            UpdatedAt = updatedAt,
        };
        DbContext.BackgroundTaskStates.AddRange(betaTask, alphaTask);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new BackgroundTaskStateReadModel(
                alphaTask.Id,
                "alpha-task",
                "Alpha task",
                true,
                defaultIntervalAboveOneDay,
                intervalOverride,
                lastStartedAt,
                lastFinishedAt,
                BackgroundTaskExecutionStatus.Error,
                "Résumé import failed",
                updatedAt
            ),
            new BackgroundTaskStateReadModel(
                betaTask.Id,
                "beta-task",
                "Beta task",
                false,
                TimeSpan.FromMinutes(15),
                null,
                null,
                null,
                null,
                null,
                updatedAt
            ),
        ]);
    }

    [Test]
    public async Task GetAllAsync_IntervalOverrideAboveOneDay_ReturnsExactOverride()
    {
        // Arrange
        var intervalOverride = new TimeSpan(
            days: 3,
            hours: 0,
            minutes: 0,
            seconds: 1,
            milliseconds: 5
        );
        DbContext.BackgroundTaskStates.Add(
            new BackgroundTaskState
            {
                Key = "alpha-task",
                DisplayName = "Alpha task",
                DefaultInterval = TimeSpan.FromSeconds(30),
                IntervalOverride = intervalOverride,
                UpdatedAt = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc),
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.Single().DefaultInterval.ShouldBe(TimeSpan.FromSeconds(30));
        result.Single().IntervalOverride.ShouldBe(intervalOverride);
    }
}
