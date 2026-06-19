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

public class BackgroundTaskStateServiceTest : BearcatIntegrationTest
{
    private const string Key = "release-import";
    private const string DisplayName = "Release import";

    private BearcatDbContext dbContext = null!;
    private Mock<IBackgroundTaskScheduleCache> scheduleCacheMock = null!;
    private BackgroundTaskStateService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        scheduleCacheMock = new Mock<IBackgroundTaskScheduleCache>(MockBehavior.Strict);

        service = new BackgroundTaskStateService(
            new BackgroundTaskStateRepository(dbContext, dbContext),
            scheduleCacheMock.Object,
            CreateTimeProvider()
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task RegisterAsync_TaskDoesNotExist_CreatesTaskAndPrimesCache()
    {
        // Arrange
        scheduleCacheMock.Setup(cache => cache.SetEnabled(Key, true));
        scheduleCacheMock.Setup(cache => cache.SetOverride(Key, null));

        // Act
        var isEnabled = await service.RegisterAsync(
            Key,
            DisplayName,
            TimeSpan.FromMinutes(15),
            CancellationToken.None
        );

        // Assert
        isEnabled.ShouldBeTrue();

        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.Key.ShouldBe(Key);
        taskState.DisplayName.ShouldBe(DisplayName);
        taskState.IsEnabled.ShouldBeTrue();
        taskState.DefaultInterval.ShouldBe(TimeSpan.FromMinutes(15));
        scheduleCacheMock.Verify(cache => cache.SetEnabled(Key, true), Times.Once);
        scheduleCacheMock.Verify(cache => cache.SetOverride(Key, null), Times.Once);
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
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
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
        dbContext.Update(existing);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        await service.MarkStartedAsync(Key, DisplayName, CancellationToken.None);

        // Assert
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.LastStartedAt.ShouldNotBeNull();
        taskState.LastFinishedAt.ShouldBeNull();
        taskState.LastExecutionStatus.ShouldBeNull();
        taskState.LastErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task MarkSucceededAsync_TaskExists_SetsSuccessStatus()
    {
        // Arrange
        await AddTaskStateAsync();

        // Act
        await service.MarkSucceededAsync(Key, DisplayName, CancellationToken.None);

        // Assert
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.LastFinishedAt.ShouldNotBeNull();
        taskState.LastExecutionStatus.ShouldBe(BackgroundTaskExecutionStatus.Success);
        taskState.LastErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task MarkFailedAsync_TaskExists_SetsErrorStatusWithMessage()
    {
        // Arrange
        await AddTaskStateAsync();

        // Act
        await service.MarkFailedAsync(
            Key,
            DisplayName,
            new InvalidOperationException("boom"),
            CancellationToken.None
        );

        // Assert
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.LastFinishedAt.ShouldNotBeNull();
        taskState.LastExecutionStatus.ShouldBe(BackgroundTaskExecutionStatus.Error);
        taskState.LastErrorMessage.ShouldBe("boom");
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
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.LastErrorMessage!.Length.ShouldBe(2000);
    }

    [Test]
    public async Task MarkStartedAsync_TaskDoesNotExist_CreatesTaskWithZeroDefaultInterval()
    {
        // Act
        await service.MarkStartedAsync(Key, DisplayName, CancellationToken.None);

        // Assert
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.DefaultInterval.ShouldBe(TimeSpan.Zero);
        taskState.LastStartedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task SetIsEnabledAsync_TaskExists_UpdatesStateAndCache()
    {
        // Arrange
        var existing = await AddTaskStateAsync(isEnabled: true);
        scheduleCacheMock.Setup(cache => cache.SetEnabled(Key, false));

        // Act
        await service.SetIsEnabledAsync(existing.Id, false, CancellationToken.None);

        // Assert
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.IsEnabled.ShouldBeFalse();
        scheduleCacheMock.Verify(cache => cache.SetEnabled(Key, false), Times.Once);
    }

    [Test]
    public async Task SetIntervalOverrideAsync_ValidInterval_UpdatesStateAndCache()
    {
        // Arrange
        var existing = await AddTaskStateAsync();
        var interval = TimeSpan.FromMinutes(10);
        scheduleCacheMock.Setup(cache => cache.SetOverride(Key, interval));

        // Act
        await service.SetIntervalOverrideAsync(existing.Id, interval, CancellationToken.None);

        // Assert
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.IntervalOverride.ShouldBe(interval);
        scheduleCacheMock.Verify(cache => cache.SetOverride(Key, interval), Times.Once);
    }

    [Test]
    public async Task SetIntervalOverrideAsync_NullInterval_ClearsOverride()
    {
        // Arrange
        var existing = await AddTaskStateAsync(intervalOverride: TimeSpan.FromMinutes(10));
        scheduleCacheMock.Setup(cache => cache.SetOverride(Key, null));

        // Act
        await service.SetIntervalOverrideAsync(existing.Id, null, CancellationToken.None);

        // Assert
        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
        taskState.IntervalOverride.ShouldBeNull();
        scheduleCacheMock.Verify(cache => cache.SetOverride(Key, null), Times.Once);
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

        var taskState = await dbContext.BackgroundTaskStates.SingleAsync();
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

        dbContext.BackgroundTaskStates.Add(taskState);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

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
