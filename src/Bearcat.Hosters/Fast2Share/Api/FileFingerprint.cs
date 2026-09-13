using System.Buffers;
using System.Security.Cryptography;
using Bearcat.Abstractions;

namespace Bearcat.Hosters.Fast2Share.Api;

public static class FileFingerprint
{
    private const int SegmentSize = 8 * 1024 * 1024;

    private const int BufferSize = 1024 * 1024;

    public static async Task<string> ComputeFingerprintAsync(
        string fullFileName,
        CancellationToken cancellationToken
    )
    {
        await using var stream = SequentialFileReader.OpenRead(fullFileName);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            var fileSize = stream.Length;
            var firstSegmentLength = Math.Min(SegmentSize, fileSize);
            await AppendAsync(stream, hash, firstSegmentLength, buffer, cancellationToken);

            if (fileSize > SegmentSize)
            {
                var lastSegmentLength = Math.Min(SegmentSize, fileSize - SegmentSize);
                stream.Seek(fileSize - lastSegmentLength, SeekOrigin.Begin);
                await AppendAsync(stream, hash, lastSegmentLength, buffer, cancellationToken);
            }

            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<string> ComputeSha256Async(
        string fullFileName,
        CancellationToken cancellationToken
    )
    {
        await using var stream = SequentialFileReader.OpenRead(fullFileName);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);

        return Convert.ToHexStringLower(hashBytes);
    }

    private static async Task AppendAsync(
        Stream stream,
        IncrementalHash hash,
        long length,
        byte[] buffer,
        CancellationToken cancellationToken
    )
    {
        var remainingBytes = length;

        while (remainingBytes > 0)
        {
            var bytesToRead = (int)Math.Min(buffer.Length, remainingBytes);
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(0, bytesToRead),
                cancellationToken
            );

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    "Fast2Share file stream ended before the fingerprint was fully read"
                );
            }

            hash.AppendData(buffer, 0, bytesRead);
            remainingBytes -= bytesRead;
        }
    }
}
