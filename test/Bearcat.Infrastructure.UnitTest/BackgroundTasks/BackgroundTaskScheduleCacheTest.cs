using Bearcat.Infrastructure.BackgroundTasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.BackgroundTasks;

public class BackgroundTaskScheduleCacheTest
{
    private const string Key = "archive-upload";

    private BackgroundTaskScheduleCache cache = null!;

    [SetUp]
    public void Setup()
    {
        cache = new BackgroundTaskScheduleCache(Mock.Of<IServiceScopeFactory>());
    }

    [TearDown]
    public void TearDown()
    {
        cache.Dispose();
    }

    [Test]
    public void SetEnabled_DisablingTaskWithRunningTick_CancelsRunningTick()
    {
        // Arrange
        var tickCancellationToken = cache.RegisterRunningTick(Key, CancellationToken.None);

        // Act
        cache.SetEnabled(Key, false);

        // Assert
        tickCancellationToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Test]
    public void SetEnabled_DisablingOtherTask_DoesNotCancelRunningTick()
    {
        // Arrange
        var tickCancellationToken = cache.RegisterRunningTick(Key, CancellationToken.None);

        // Act
        cache.SetEnabled("other-task", false);

        // Assert
        tickCancellationToken.IsCancellationRequested.ShouldBeFalse();
    }

    [Test]
    public void RegisterRunningTick_TaskAlreadyDisabled_ReturnsCanceledToken()
    {
        // Arrange
        cache.SetEnabled(Key, false);

        // Act
        var tickCancellationToken = cache.RegisterRunningTick(Key, CancellationToken.None);

        // Assert
        tickCancellationToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Test]
    public void SetEnabled_DisablingTaskAfterTickWasUnregistered_DoesNotThrow()
    {
        // Arrange
        cache.RegisterRunningTick(Key, CancellationToken.None);
        cache.UnregisterRunningTick(Key);

        // Act
        var disable = () => cache.SetEnabled(Key, false);

        // Assert
        disable.ShouldNotThrow();
    }
}
