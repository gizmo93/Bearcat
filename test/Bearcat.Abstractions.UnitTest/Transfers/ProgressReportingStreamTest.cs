using Bearcat.Abstractions.Transfers;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Transfers;

public class ProgressReportingStreamTest
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
        await using var stream = new ProgressReportingStream(
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
        await using var stream = new ProgressReportingStream(
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
    public async Task ReadAsync_SpeedLimitScope_ClampsReadToSmallestMaximumBytesPerAcquire()
    {
        // Arrange
        using var hosterLimiter = new TransferSpeedLimiter(bytesPerSecond: 200);
        using var globalLimiter = new TransferSpeedLimiter(bytesPerSecond: 100);
        var stream = CreateStreamInScope([hosterLimiter, globalLimiter]);
        var buffer = new byte[4096];

        // Act
        var bytesRead = await stream.ReadAsync(buffer);

        // Assert
        bytesRead.ShouldBe(100);
        buffer[..100].ShouldBe(SourceBytes[..100]);
    }

    [Test]
    public void Read_SpeedLimitScope_ClampsReadToMaximumBytesPerAcquire()
    {
        // Arrange
        using var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);
        var stream = CreateStreamInScope([limiter]);
        var buffer = new byte[4096];

        // Act
        var bytesRead = stream.Read(buffer, 0, buffer.Length);

        // Assert
        bytesRead.ShouldBe(100);
    }

    [Test]
    public async Task ReadAsync_InnerStreamReturnsFewerBytes_AcquiresOnlyBytesActuallyRead()
    {
        // Arrange
        using var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);
        var stream = CreateStreamInScope(
            [limiter],
            new ShortReadingStream(SourceBytes, maximumBytesPerRead: 10)
        );
        var buffer = new byte[4096];

        // Act
        var bytesRead = await stream.ReadAsync(buffer);

        // Assert
        bytesRead.ShouldBe(10);
        limiter.AcquireAsync(90, CancellationToken.None).IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Test]
    public void Read_InnerStreamReturnsFewerBytes_AcquiresOnlyBytesActuallyRead()
    {
        // Arrange
        using var limiter = new TransferSpeedLimiter(bytesPerSecond: 100);
        var stream = CreateStreamInScope(
            [limiter],
            new ShortReadingStream(SourceBytes, maximumBytesPerRead: 10)
        );
        var buffer = new byte[4096];

        // Act
        var bytesRead = stream.Read(buffer, 0, buffer.Length);

        // Assert
        bytesRead.ShouldBe(10);
        limiter.AcquireAsync(90, CancellationToken.None).IsCompletedSuccessfully.ShouldBeTrue();
    }

    private static ProgressReportingStream CreateStreamInScope(
        IReadOnlyList<TransferSpeedLimiter> limiters
    )
    {
        return CreateStreamInScope(limiters, new MemoryStream(SourceBytes));
    }

    private static ProgressReportingStream CreateStreamInScope(
        IReadOnlyList<TransferSpeedLimiter> limiters,
        Stream innerStream
    )
    {
        using var scope = TransferSpeedLimitScope.Enter(limiters);

        return new ProgressReportingStream(
            innerStream,
            new RecordingTransferProgress(),
            SourceBytes.Length
        );
    }

    private sealed class ShortReadingStream(byte[] bytes, int maximumBytesPerRead)
        : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, Math.Min(count, maximumBytesPerRead));
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            return base.ReadAsync(
                buffer[..Math.Min(buffer.Length, maximumBytesPerRead)],
                cancellationToken
            );
        }
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
