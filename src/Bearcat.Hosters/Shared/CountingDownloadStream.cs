using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Shared;

public sealed class CountingDownloadStream : Stream
{
    private readonly Stream inner;

    private readonly IDownloadProgress progress;

    public CountingDownloadStream(Stream inner, IDownloadProgress progress, long? totalBytes)
    {
        this.inner = inner;
        this.progress = progress;
        progress.BeginFile(totalBytes);
    }

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = inner.Read(buffer, offset, count);
        Report(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var bytesRead = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        Report(bytesRead);
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var bytesRead = await inner.ReadAsync(buffer, cancellationToken);
        Report(bytesRead);
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void Flush() => inner.Flush();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private void Report(int bytesRead)
    {
        if (bytesRead > 0)
        {
            progress.ReportBytesTransferred(bytesRead);
        }
    }
}
