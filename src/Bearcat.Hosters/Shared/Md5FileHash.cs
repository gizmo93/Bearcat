using System.Security.Cryptography;

namespace Bearcat.Hosters.Shared;

public static class Md5FileHash
{
    public static async Task<string> GetOrComputeAsync(
        string? knownHash,
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(knownHash))
        {
            return knownHash.ToLowerInvariant();
        }

        using var md5 = MD5.Create();
        var hashBytes = await md5.ComputeHashAsync(stream, cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);

        return Convert.ToHexStringLower(hashBytes);
    }
}
