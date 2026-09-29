using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

namespace Bearcat.Domain.UseCases.ManageProxyServers.CategoryDefaults;

public class ProxyCategoryDefaultService(
    IProxyCategoryDefaultWriteRepository writeRepository,
    IProxyRoutingCache proxyRoutingCache
)
{
    public async Task SetDefaultAsync(
        ProxyCategory category,
        int? proxyServerId,
        CancellationToken cancellationToken = default
    )
    {
        await SetDefaultsAsync([category], proxyServerId, cancellationToken);
    }

    public async Task SetDefaultForAllCategoriesAsync(
        int? proxyServerId,
        CancellationToken cancellationToken = default
    )
    {
        await SetDefaultsAsync(Enum.GetValues<ProxyCategory>(), proxyServerId, cancellationToken);
    }

    private async Task SetDefaultsAsync(
        IReadOnlyList<ProxyCategory> categories,
        int? proxyServerId,
        CancellationToken cancellationToken
    )
    {
        var existingDefaults = (
            await writeRepository.GetAllForUpdateAsync(cancellationToken)
        ).ToDictionary(proxyCategoryDefault => proxyCategoryDefault.ProxyCategory);

        foreach (var category in categories)
        {
            if (existingDefaults.TryGetValue(category, out var existingDefault))
            {
                existingDefault.ProxyServerId = proxyServerId;
                continue;
            }

            writeRepository.Add(
                new ProxyCategoryDefault { ProxyCategory = category, ProxyServerId = proxyServerId }
            );
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
        await proxyRoutingCache.RefreshAsync(cancellationToken);
    }
}
