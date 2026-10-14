using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithStorageQuota : IHoster
{
    Task<HosterStorageQuota> GetStorageQuotaAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );
}
