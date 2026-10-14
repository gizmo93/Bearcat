using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Domain.UseCases.ManageUploads.HosterAccountPoolSelection;

public record HosterStorageAvailability(
    bool QuotaRequestFailed,
    bool IsUnlimited,
    long AvailableBytes,
    DateTime ExpiresAt
)
{
    public bool CanStore(long bytes) =>
        !QuotaRequestFailed && (IsUnlimited || AvailableBytes >= bytes);

    public static HosterStorageAvailability FromQuota(
        HosterStorageQuota quota,
        long reservedBytes,
        DateTime expiresAt
    ) =>
        new(
            QuotaRequestFailed: false,
            IsUnlimited: quota.IsUnlimited,
            AvailableBytes: quota.FreeBytes - reservedBytes,
            ExpiresAt: expiresAt
        );

    public static HosterStorageAvailability ForFailedQuotaRequest(DateTime expiresAt) =>
        new(QuotaRequestFailed: true, IsUnlimited: false, AvailableBytes: 0, ExpiresAt: expiresAt);
}
