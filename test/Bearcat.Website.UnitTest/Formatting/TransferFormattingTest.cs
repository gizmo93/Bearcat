using Bearcat.Domain.Shared.Transfers;
using Bearcat.Website.Formatting;
using Shouldly;

namespace Bearcat.Website.UnitTest.Formatting;

[SetCulture("en-US")]
public class TransferFormattingTest
{
    [TestCase(1_000_000, 0, 10_000, "1 minute")]
    [TestCase(1_000_000, 400_000, 10_000, "1 minute")]
    [TestCase(10_000_000, 0, 10_000, "16 minutes")]
    [TestCase(100_000_000, 0, 10_000, "2 hours")]
    [TestCase(1_000, 0, 100, "10 seconds")]
    [TestCase(1_000, 990, 100, "1 second")]
    public void FormatRemainingTime_SpeedAndTotalKnown_ReturnsHumanizedRemainingTime(
        long totalBytes,
        long transferredBytes,
        double bytesPerSecond,
        string expectedRemainingTime
    )
    {
        // Arrange
        var snapshot = CreateSnapshot(totalBytes, transferredBytes, bytesPerSecond);

        // Act
        var remainingTime = TransferFormatting.FormatRemainingTime(snapshot);

        // Assert
        remainingTime.ShouldBe(expectedRemainingTime);
    }

    [TestCase(1_000, 0, 0)]
    [TestCase(0, 500, 100)]
    [TestCase(1_000, 1_000, 100)]
    public void FormatRemainingTime_SpeedOrRemainingBytesUnknownOrZero_ReturnsNull(
        long totalBytes,
        long transferredBytes,
        double bytesPerSecond
    )
    {
        // Arrange
        var snapshot = CreateSnapshot(totalBytes, transferredBytes, bytesPerSecond);

        // Act
        var remainingTime = TransferFormatting.FormatRemainingTime(snapshot);

        // Assert
        remainingTime.ShouldBeNull();
    }

    private static TransferProgressSnapshot CreateSnapshot(
        long totalBytes,
        long transferredBytes,
        double bytesPerSecond
    )
    {
        return new TransferProgressSnapshot(
            Identifier: new TransferIdentifier(TransferType.Upload, 1),
            BytesPerSecond: bytesPerSecond,
            TransferredBytes: transferredBytes,
            TotalBytes: totalBytes,
            Files: []
        );
    }
}
