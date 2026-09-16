using System.Security.Cryptography;
using Bearcat.Abstractions;

namespace Bearcat.Domain.Shared;

public static class Md5FileHash
{
    public static async Task<string> ComputeAsync(
        string fullFileName,
        CancellationToken cancellationToken
    )
    {
        await using var stream = SequentialFileReader.OpenRead(fullFileName);
        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);

        return Convert.ToHexString(hash);
    }
}
