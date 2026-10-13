namespace Bearcat.Abstractions.Hoster.Results;

public record HosterStorageQuota(bool IsUnlimited, long FreeBytes, long? TotalBytes)
{
    public static HosterStorageQuota Unlimited()
    {
        return new HosterStorageQuota(IsUnlimited: true, FreeBytes: 0, TotalBytes: null);
    }

    public static HosterStorageQuota Limited(long freeBytes, long? totalBytes)
    {
        return new HosterStorageQuota(
            IsUnlimited: false,
            FreeBytes: freeBytes,
            TotalBytes: totalBytes
        );
    }
}
