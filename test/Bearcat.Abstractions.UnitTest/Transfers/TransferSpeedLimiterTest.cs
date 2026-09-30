using Bearcat.Abstractions.Transfers;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Transfers;

public class TransferSpeedLimiterTest
{
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);

    [Test]
    public void AcquireAsync_TokensAvailable_CompletesSynchronously()
    {
        // Arrange
        using var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);

        // Act
        var acquireTask = limiter.AcquireAsync(100, CancellationToken.None);

        // Assert
        acquireTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Test]
    public void AcquireAsync_ByteCountAboveTokenLimit_IsClampedToTokenLimit()
    {
        // Arrange
        using var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);

        // Act
        var acquireTask = limiter.AcquireAsync(10_000, CancellationToken.None);

        // Assert
        acquireTask.IsCompletedSuccessfully.ShouldBeTrue();
        limiter.MaximumBytesPerAcquire.ShouldBe(100);
    }

    [Test]
    public async Task AcquireAsync_MoreBytesThanAvailableTokens_Waits()
    {
        // Arrange
        var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);
        await limiter.AcquireAsync(100, CancellationToken.None);

        // Act
        var waitingTask = limiter.AcquireAsync(100, CancellationToken.None).AsTask();

        // Assert
        waitingTask.IsCompleted.ShouldBeFalse();
        limiter.Dispose();
        await waitingTask.WaitAsync(CompletionTimeout);
    }

    [Test]
    public async Task ChangeBytesPerSecond_WhileWaiterIsQueued_WaiterCompletesOnNewLimiter()
    {
        // Arrange
        using var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);
        await limiter.AcquireAsync(100, CancellationToken.None);
        var waitingTask = limiter.AcquireAsync(100, CancellationToken.None).AsTask();
        waitingTask.IsCompleted.ShouldBeFalse();

        // Act
        limiter.ChangeBytesPerSecond(1_000_000);

        // Assert
        await Should.NotThrowAsync(() => waitingTask.WaitAsync(CompletionTimeout));
        limiter.BytesPerSecond.ShouldBe(1_000_000);
        limiter.MaximumBytesPerAcquire.ShouldBe(1_000_000);
    }

    [Test]
    public async Task Dispose_WhileWaiterIsQueued_WaiterCompletes()
    {
        // Arrange
        var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);
        await limiter.AcquireAsync(100, CancellationToken.None);
        var waitingTask = limiter.AcquireAsync(100, CancellationToken.None).AsTask();

        // Act
        limiter.Dispose();

        // Assert
        await Should.NotThrowAsync(() => waitingTask.WaitAsync(CompletionTimeout));
    }
}
