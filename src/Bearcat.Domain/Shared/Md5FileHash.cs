using System.Buffers;
using System.Security.Cryptography;
using Bearcat.Abstractions;

namespace Bearcat.Domain.Shared;

public static class Md5FileHash
{
    private const int BufferSize = 4 * 1024 * 1024;

    public static async Task<string> ComputeAsync(
        string fullFileName,
        CancellationToken cancellationToken
    )
    {
        await using var stream = SequentialFileReader.OpenRead(fullFileName);
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                incrementalHash.AppendData(buffer, 0, bytesRead);
            }

            return Convert.ToHexString(incrementalHash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
