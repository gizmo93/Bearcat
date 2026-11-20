using Bearcat.Abstractions.Transfers;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Transfers;

public class TransferSpeedLimiterTest
{
    [Test]
    public void WaitForBytesAsync_BytesOfOneSecond_Waits()
    {
        // Arrange
        var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);

        // Act
        var waitingTask = limiter.WaitForBytesAsync(100, CancellationToken.None).AsTask();

        // Assert
        waitingTask.IsCompleted.ShouldBeFalse();
    }

    [Test]
    public void WaitForBytesAsync_BytesFarBelowRate_CompletesSynchronously()
    {
        // Arrange
        var limiter = new TransferSpeedLimiter(bytesPerSecond: 1_000_000);

        // Act
        var waitingTask = limiter.WaitForBytesAsync(100, CancellationToken.None).AsTask();

        // Assert
        waitingTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Test]
    public void WaitForBytesAsync_ManySmallBlocks_WaitsOnceTheirDurationsAddUp()
    {
        // Arrange
        var limiter = new TransferSpeedLimiter(bytesPerSecond: 1000);
        var firstTask = limiter.WaitForBytesAsync(2, CancellationToken.None).AsTask();
        var secondTask = limiter.WaitForBytesAsync(2, CancellationToken.None).AsTask();

        // Act
        var thirdTask = limiter.WaitForBytesAsync(2, CancellationToken.None).AsTask();

        // Assert
        firstTask.IsCompletedSuccessfully.ShouldBeTrue();
        secondTask.IsCompletedSuccessfully.ShouldBeTrue();
        thirdTask.IsCompleted.ShouldBeFalse();
    }

    [Test]
    public async Task WaitForBytesAsync_CancelledWhileWaiting_ThrowsOperationCanceledException()
    {
        // Arrange
        var limiter = new TransferSpeedLimiter(bytesPerSecond: 1);
        using var cancellationTokenSource = new CancellationTokenSource();
        var waitingTask = limiter.WaitForBytesAsync(1000, cancellationTokenSource.Token).AsTask();

        // Act
        await cancellationTokenSource.CancelAsync();

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(() => waitingTask);
    }

    [Test]
    public void ChangeBytesPerSecond_AfterSlowTransfers_NextBlockUsesOnlyNewRate()
    {
        // Arrange
        var limiter = new TransferSpeedLimiter(bytesPerSecond: 1);
        _ = limiter.WaitForBytesAsync(1000, CancellationToken.None).AsTask();

        // Act
        limiter.ChangeBytesPerSecond(1_000_000);

        // Assert
        limiter.BytesPerSecond.ShouldBe(1_000_000);
        limiter
            .WaitForBytesAsync(100, CancellationToken.None)
            .AsTask()
            .IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Test]
    public void StopLimiting_AfterSlowTransfers_NextBlockCompletesSynchronously()
    {
        // Arrange
        var limiter = new TransferSpeedLimiter(bytesPerSecond: 1);
        _ = limiter.WaitForBytesAsync(1000, CancellationToken.None).AsTask();

        // Act
        limiter.StopLimiting();

        // Assert
        limiter
            .WaitForBytesAsync(1_000_000, CancellationToken.None)
            .AsTask()
            .IsCompletedSuccessfully.ShouldBeTrue();
    }
}
