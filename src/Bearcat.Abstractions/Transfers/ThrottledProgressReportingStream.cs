namespace Bearcat.Abstractions.Transfers;

public sealed class ThrottledProgressReportingStream : Stream
{
    private readonly Stream inner;

    private readonly ITransferProgress progress;

    private readonly IReadOnlyList<TransferSpeedLimiter> speedLimiters;

    public ThrottledProgressReportingStream(
        Stream inner,
        ITransferProgress progress,
        long? totalBytes
    )
    {
        this.inner = inner;
        this.progress = progress;
        speedLimiters = TransferSpeedLimitScope.Current;
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

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

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
        var readLength = buffer.Length;

        foreach (var speedLimiter in speedLimiters)
        {
            readLength = Math.Min(readLength, speedLimiter.BytesPerSecond);
        }

        var bytesRead = await inner.ReadAsync(buffer[..readLength], cancellationToken);

        if (bytesRead == 0)
        {
            return 0;
        }

        foreach (var speedLimiter in speedLimiters)
        {
            await speedLimiter.WaitForBytesAsync(bytesRead, cancellationToken);
        }

        progress.ReportBytesTransferred(bytesRead);

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void Flush() => inner.Flush();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
