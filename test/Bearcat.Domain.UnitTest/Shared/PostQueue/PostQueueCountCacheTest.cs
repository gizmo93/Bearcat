using Bearcat.Domain.Shared.PostQueue;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.PostQueue;

public class PostQueueCountCacheTest
{
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    [Test]
    public async Task GetOrCountOpenItemsAsync_CalledTwiceWithinMaximumCountAge_CountsOnceAndReturnsCachedCount()
    {
        // Arrange
        using var cache = new PostQueueCountCache(OneHour);
        var countingCallCount = 0;

        // Act
        var firstResult = await cache.GetOrCountOpenItemsAsync(
            _ => Task.FromResult(++countingCallCount * 10),
            CancellationToken.None
        );
        var secondResult = await cache.GetOrCountOpenItemsAsync(
            _ => Task.FromResult(++countingCallCount * 10),
            CancellationToken.None
        );

        // Assert
        firstResult.ShouldBe(10);
        secondResult.ShouldBe(10);
        countingCallCount.ShouldBe(1);
    }

    [Test]
    public async Task GetOrCountOpenItemsAsync_CachedCountIsOlderThanMaximumCountAge_CountsAgain()
    {
        // Arrange
        using var cache = new PostQueueCountCache(TimeSpan.Zero);
        var countingCallCount = 0;

        // Act
        await cache.GetOrCountOpenItemsAsync(
            _ => Task.FromResult(++countingCallCount * 10),
            CancellationToken.None
        );
        var secondResult = await cache.GetOrCountOpenItemsAsync(
            _ => Task.FromResult(++countingCallCount * 10),
            CancellationToken.None
        );

        // Assert
        secondResult.ShouldBe(20);
        countingCallCount.ShouldBe(2);
    }

    [Test]
    public async Task GetOrCountOpenItemsAsync_CacheWasInvalidated_CountsAgain()
    {
        // Arrange
        using var cache = new PostQueueCountCache(OneHour);
        var countingCallCount = 0;
        await cache.GetOrCountOpenItemsAsync(
            _ => Task.FromResult(++countingCallCount * 10),
            CancellationToken.None
        );

        // Act
        cache.Invalidate();
        var result = await cache.GetOrCountOpenItemsAsync(
            _ => Task.FromResult(++countingCallCount * 10),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(20);
        countingCallCount.ShouldBe(2);
    }

    [Test]
    public async Task GetOrCountOpenItemsAsync_SeveralConcurrentCallers_CountsOnceAndReturnsSameCountToAllCallers()
    {
        // Arrange
        using var cache = new PostQueueCountCache(OneHour);
        var countingCallCount = 0;
        var countingStarted = new TaskCompletionSource();
        var countingMayFinish = new TaskCompletionSource();

        async Task<int> CountOpenItemsAsync(CancellationToken _)
        {
            Interlocked.Increment(ref countingCallCount);
            countingStarted.TrySetResult();
            await countingMayFinish.Task;
            return 7;
        }

        // Act
        var firstCall = cache.GetOrCountOpenItemsAsync(CountOpenItemsAsync, CancellationToken.None);
        await countingStarted.Task;
        var waitingCalls = Enumerable
            .Range(1, 5)
            .Select(_ =>
                cache.GetOrCountOpenItemsAsync(CountOpenItemsAsync, CancellationToken.None)
            )
            .ToList();
        countingMayFinish.SetResult();
        var results = await Task.WhenAll([firstCall, .. waitingCalls]);

        // Assert
        results.ShouldAllBe(result => result == 7);
        countingCallCount.ShouldBe(1);
    }

    [Test]
    public async Task GetOrCountOpenItemsAsync_CacheIsInvalidatedWhileCounting_DoesNotCacheCountOfRunningCounting()
    {
        // Arrange
        using var cache = new PostQueueCountCache(OneHour);
        var countingCallCount = 0;

        // Act
        var firstResult = await cache.GetOrCountOpenItemsAsync(
            _ =>
            {
                countingCallCount++;
                cache.Invalidate();
                return Task.FromResult(10);
            },
            CancellationToken.None
        );
        var secondResult = await cache.GetOrCountOpenItemsAsync(
            _ => Task.FromResult(++countingCallCount * 10),
            CancellationToken.None
        );

        // Assert
        firstResult.ShouldBe(10);
        secondResult.ShouldBe(20);
        countingCallCount.ShouldBe(2);
    }

    [Test]
    public async Task GetOrCountOpenItemsAsync_CountingFails_DoesNotCacheAndCountsAgainOnNextCall()
    {
        // Arrange
        using var cache = new PostQueueCountCache(OneHour);

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() =>
            cache.GetOrCountOpenItemsAsync(
                _ => throw new InvalidOperationException("Database is not reachable"),
                CancellationToken.None
            )
        );
        var result = await cache.GetOrCountOpenItemsAsync(
            _ => Task.FromResult(3),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(3);
    }
}
