using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Domain.UseCases.ManageHosters;

public record StorageQuotaCheckResult(
    bool IsSuccess,
    HosterStorageQuota? StorageQuota,
    string? ErrorMessage
);
