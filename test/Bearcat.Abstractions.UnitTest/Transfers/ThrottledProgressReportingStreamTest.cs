using Bearcat.Abstractions.Transfers;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Transfers;

public class ThrottledProgressReportingStreamTest
{
    private static readonly byte[] SourceBytes = Enumerable
        .Range(0, 1000)
        .Select(index => (byte)index)
        .ToArray();

    [Test]
    public async Task CopyToAsync_NoSpeedLimitScope_CopiesIdenticalBytesAndReportsProgress()
    {
        // Arrange
        var progress = new RecordingTransferProgress();
        await using var stream = new ThrottledProgressReportingStream(
            new MemoryStream(SourceBytes),
            progress,
            SourceBytes.Length
        );
        using var target = new MemoryStream();

        // Act
        await stream.CopyToAsync(target);

        // Assert
        target.ToArray().ShouldBe(SourceBytes);
        progress.TransferredBytes.ShouldBe(SourceBytes.Length);
    }

    [Test]
    public async Task ReadAsync_NoSpeedLimitScope_ReadsFullRequestedCount()
    {
        // Arrange
        await using var stream = new ThrottledProgressReportingStream(
            new MemoryStream(SourceBytes),
            new RecordingTransferProgress(),
            SourceBytes.Length
        );
        var buffer = new byte[4096];

        // Act
        var bytesRead = await stream.ReadAsync(buffer);

        // Assert
        bytesRead.ShouldBe(SourceBytes.Length);
    }

    [Test]
    public async Task ReadAsync_SpeedLimitScope_ClampsReadToSmallestBytesPerSecond()
    {
        // Arrange
        var hosterLimiter = new TransferSpeedLimiter(bytesPerSecond: 200);
        var globalLimiter = new TransferSpeedLimiter(bytesPerSecond: 100);
        var stream = CreateStreamInScope([hosterLimiter, globalLimiter]);
        var buffer = new byte[4096];

        // Act
        var bytesRead = await stream.ReadAsync(buffer);

        // Assert
        bytesRead.ShouldBe(100);
        buffer[..100].ShouldBe(SourceBytes[..100]);
    }

    private static ThrottledProgressReportingStream CreateStreamInScope(
        IReadOnlyList<TransferSpeedLimiter> limiters
    )
    {
        using var scope = TransferSpeedLimitScope.Enter(limiters);

        return new ThrottledProgressReportingStream(
            new MemoryStream(SourceBytes),
            new RecordingTransferProgress(),
            SourceBytes.Length
        );
    }

    private sealed class RecordingTransferProgress : ITransferProgress
    {
        public long TransferredBytes { get; private set; }

        public void BeginFile(long? totalBytes) { }

        public void ReportBytesTransferred(long bytes)
        {
            TransferredBytes += bytes;
        }
    }
}
