using Bearcat.Abstractions.BackgroundTasks;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageBackgroundTasks;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageBackgroundTasks;

public class BackgroundTaskStateServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string Key = "release-import";
    private const string DisplayName = "Release import";

    private Mock<IBackgroundTaskScheduleCache> scheduleCacheMock = null!;
    private BackgroundTaskStateService service = null!;

    [SetUp]
    public void Setup()
    {
        scheduleCacheMock = new Mock<IBackgroundTaskScheduleCache>(MockBehavior.Strict);

        service = new BackgroundTaskStateService(
            new BackgroundTaskStateRepository(DbContext, DbContext),
            scheduleCacheMock.Object,
            CreateTimeProvider()
        );
    }

    [Test]
    public async Task RegisterAsync_TaskExistsWithChangedMetadata_UpdatesDisplayNameAndInterval()
    {
        // Arrange
        await AddTaskStateAsync(displayName: "Old name", defaultInterval: TimeSpan.FromMinutes(5));
        scheduleCacheMock.Setup(cache => cache.SetEnabled(Key, true));
        scheduleCacheMock.Setup(cache => cache.SetOverride(Key, null));

        // Act
        await service.RegisterAsync(
            Key,
            "New name",
            TimeSpan.FromMinutes(30),
            CancellationToken.None
        );

        // Assert
        var taskState = await DbContext.BackgroundTaskStates.SingleAsync();
        taskState.DisplayName.ShouldBe("New name");
        taskState.DefaultInterval.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Test]
    public async Task RegisterAsync_TaskExistsWithOverride_ReturnsPersistedStateAndPrimesCache()
    {
        // Arrange
        await AddTaskStateAsync(
            displayName: DisplayName,
            defaultInterval: TimeSpan.FromMinutes(15),
            isEnabled: false,
            intervalOverride: TimeSpan.FromMinutes(20)
        );
        scheduleCacheMock.Setup(cache => cache.SetEnabled(Key, false));
        scheduleCacheMock.Setup(cache => cache.SetOverride(Key, TimeSpan.FromMinutes(20)));

        // Act
        var isEnabled = await service.RegisterAsync(
            Key,
            DisplayName,
            TimeSpan.FromMinutes(15),
            CancellationToken.None
        );

        // Assert
        isEnabled.ShouldBeFalse();
        scheduleCacheMock.Verify(cache => cache.SetEnabled(Key, false), Times.Once);
        scheduleCacheMock.Verify(
            cache => cache.SetOverride(Key, TimeSpan.FromMinutes(20)),
            Times.Once
        );
    }

    [Test]
    public async Task MarkStartedAsync_TaskExists_SetsStartedAndClearsResult()
    {
        // Arrange
        var existing = await AddTaskStateAsync();
        existing.LastFinishedAt = DateTime.UtcNow;
        existing.LastExecutionStatus = BackgroundTaskExecutionStatus.Success;
        existing.LastErrorMessage = "stale";
        DbContext.Update(existing);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await service.MarkStartedAsync(Key, DisplayName, CancellationToken.None);

        // Assert
        var taskState = await DbContext.BackgroundTaskStates.SingleAsync();
        taskState.LastStartedAt.ShouldNotBeNull();
        taskState.LastFinishedAt.ShouldBeNull();
        taskState.LastExecutionStatus.ShouldBeNull();
        taskState.LastErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task MarkFailedAsync_LongErrorMessage_TruncatesToMaxLength()
    {
        // Arrange
        await AddTaskStateAsync();
        var longMessage = new string('x', 2500);

        // Act
        await service.MarkFailedAsync(
            Key,
            DisplayName,
            new InvalidOperationException(longMessage),
            CancellationToken.None
        );

        // Assert
        var taskState = await DbContext.BackgroundTaskStates.SingleAsync();
        taskState.LastErrorMessage!.Length.ShouldBe(2000);
    }

    [Test]
    public async Task SetIntervalOverrideAsync_IntervalBelowMinimum_Throws()
    {
        // Arrange
        var existing = await AddTaskStateAsync();

        // Act / Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            service.SetIntervalOverrideAsync(
                existing.Id,
                TimeSpan.FromSeconds(1),
                CancellationToken.None
            )
        );

        var taskState = await DbContext.BackgroundTaskStates.SingleAsync();
        taskState.IntervalOverride.ShouldBeNull();
    }

    private async Task<BackgroundTaskState> AddTaskStateAsync(
        string displayName = DisplayName,
        TimeSpan? defaultInterval = null,
        bool isEnabled = true,
        TimeSpan? intervalOverride = null
    )
    {
        var taskState = new BackgroundTaskState
        {
            Key = Key,
            DisplayName = displayName,
            IsEnabled = isEnabled,
            DefaultInterval = defaultInterval ?? TimeSpan.FromMinutes(15),
            IntervalOverride = intervalOverride,
            UpdatedAt = DateTime.UtcNow,
        };

        DbContext.BackgroundTaskStates.Add(taskState);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return taskState;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
