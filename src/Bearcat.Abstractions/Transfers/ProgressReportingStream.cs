namespace Bearcat.Abstractions.Transfers;

public sealed class ProgressReportingStream : Stream
{
    private readonly Stream inner;

    private readonly ITransferProgress progress;

    private readonly IReadOnlyList<TransferSpeedLimiter>? speedLimiters =
        TransferSpeedLimitScope.Current;

    public ProgressReportingStream(Stream inner, ITransferProgress progress, long? totalBytes)
    {
        this.inner = inner;
        this.progress = progress;
        progress.BeginFile(totalBytes);
    }

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (speedLimiters is null)
        {
            var bytesRead = inner.Read(buffer, offset, count);
            Report(bytesRead);
            return bytesRead;
        }

        var clampedCount = ClampToSmallestMaximumBytesPerAcquire(speedLimiters, count);
        var limitedBytesRead = inner.Read(buffer, offset, clampedCount);

        if (limitedBytesRead > 0)
        {
            AcquireBytesOnAllLimitersAsync(speedLimiters, limitedBytesRead, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        Report(limitedBytesRead);
        return limitedBytesRead;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (speedLimiters is null)
        {
            var bytesRead = await inner.ReadAsync(buffer, cancellationToken);
            Report(bytesRead);
            return bytesRead;
        }

        var clampedLength = ClampToSmallestMaximumBytesPerAcquire(speedLimiters, buffer.Length);
        var limitedBytesRead = await inner.ReadAsync(buffer[..clampedLength], cancellationToken);

        if (limitedBytesRead > 0)
        {
            await AcquireBytesOnAllLimitersAsync(
                speedLimiters,
                limitedBytesRead,
                cancellationToken
            );
        }

        Report(limitedBytesRead);
        return limitedBytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void Flush() => inner.Flush();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private static int ClampToSmallestMaximumBytesPerAcquire(
        IReadOnlyList<TransferSpeedLimiter> limiters,
        int requestedByteCount
    )
    {
        var clampedByteCount = requestedByteCount;

        for (var index = 0; index < limiters.Count; index++)
        {
            clampedByteCount = Math.Min(clampedByteCount, limiters[index].MaximumBytesPerAcquire);
        }

        return clampedByteCount;
    }

    private static async ValueTask AcquireBytesOnAllLimitersAsync(
        IReadOnlyList<TransferSpeedLimiter> limiters,
        int byteCount,
        CancellationToken cancellationToken
    )
    {
        for (var index = 0; index < limiters.Count; index++)
        {
            await limiters[index].AcquireAsync(byteCount, cancellationToken);
        }
    }

    private void Report(int bytesRead)
    {
        if (bytesRead > 0)
        {
            progress.ReportBytesTransferred(bytesRead);
        }
    }
}
