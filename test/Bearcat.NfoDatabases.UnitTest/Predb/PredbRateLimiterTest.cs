using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Predb;
using Shouldly;

namespace Bearcat.NfoDatabases.UnitTest.Predb;

public class PredbRateLimiterTest
{
    [Test]
    public async Task WaitForSlotAsync_WithinWindowLimit_ReturnsImmediately()
    {
        // Arrange
        var rateLimiter = new PredbRateLimiter();

        // Act
        var elapsed = await MeasureAsync(async () =>
        {
            for (var index = 0; index < 25; index++)
            {
                await rateLimiter.WaitForSlotAsync(CancellationToken.None);
            }
        });

        // Assert
        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task WaitForSlotAsync_WindowExhausted_ThrowsRateLimitExceeded()
    {
        // Arrange
        var rateLimiter = new PredbRateLimiter();
        for (var index = 0; index < 25; index++)
        {
            await rateLimiter.WaitForSlotAsync(CancellationToken.None);
        }

        // Act
        var exception = await Should.ThrowAsync<NfoDatabaseRateLimitExceededException>(() =>
            rateLimiter.WaitForSlotAsync(CancellationToken.None)
        );

        // Assert
        exception.DatabaseName.ShouldBe("PreDB");
        exception.ResetAt.ShouldNotBeNull();
        exception.ResetAt.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task WaitForSlotAsync_ManyConcurrentCallers_GrantsExactlyWindowLimit()
    {
        // Arrange
        var rateLimiter = new PredbRateLimiter();

        // Act
        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 200)
                .Select(async _ =>
                {
                    try
                    {
                        await rateLimiter.WaitForSlotAsync(CancellationToken.None);
                        return true;
                    }
                    catch (NfoDatabaseRateLimitExceededException)
                    {
                        return false;
                    }
                })
        );

        // Assert
        results.Count(granted => granted).ShouldBe(25);
    }

    private static async Task<TimeSpan> MeasureAsync(Func<Task> action)
    {
        var startedAt = DateTimeOffset.UtcNow;
        await action();
        return DateTimeOffset.UtcNow - startedAt;
    }
}
