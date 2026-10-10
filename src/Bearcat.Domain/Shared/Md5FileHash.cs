using System.Buffers;
using System.Security.Cryptography;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Transfers;

namespace Bearcat.Domain.Shared;

public static class Md5FileHash
{
    private const int BufferSize = 4 * 1024 * 1024;

    public static Task<string> ComputeAsync(
        string fullFileName,
        CancellationToken cancellationToken
    )
    {
        return ComputeAsync(fullFileName, NullTransferProgress.Instance, cancellationToken);
    }

    public static async Task<string> ComputeAsync(
        string fullFileName,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        await AppendFileContentAsync(
            incrementalHash: incrementalHash,
            fullFileName: fullFileName,
            progress: progress,
            cancellationToken: cancellationToken
        );

        return Convert.ToHexString(incrementalHash.GetHashAndReset());
    }

    public static async Task<NullByteSuffixHash> ComputeHashWithShortestUnknownNullByteSuffixAsync(
        string fullFileName,
        Func<string, bool> addHashIfUnknown,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        await AppendFileContentAsync(
            incrementalHash: incrementalHash,
            fullFileName: fullFileName,
            progress: progress,
            cancellationToken: cancellationToken
        );

        byte[] nullByte = [0];
        var nullByteCount = 0;
        string hash;

        do
        {
            incrementalHash.AppendData(nullByte);
            nullByteCount++;
            hash = Convert.ToHexString(incrementalHash.GetCurrentHash());
        } while (!addHashIfUnknown(hash));

        return new NullByteSuffixHash(NullByteCount: nullByteCount, Md5Hash: hash);
    }

    private static async Task AppendFileContentAsync(
        IncrementalHash incrementalHash,
        string fullFileName,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        await using var stream = SequentialFileReader.OpenRead(fullFileName);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                incrementalHash.AppendData(buffer, 0, bytesRead);
                progress.ReportBytesTransferred(bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
