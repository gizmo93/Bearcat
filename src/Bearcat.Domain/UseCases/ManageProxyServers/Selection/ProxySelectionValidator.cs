using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Selection;

public class ProxySelectionValidator(IProxyServerReadRepository proxyServerReadRepository)
{
    public async Task<int?> GetProxyServerIdToStoreAsync(
        ProxySelection proxySelection,
        int? proxyServerId,
        CancellationToken cancellationToken
    )
    {
        if (proxySelection != ProxySelection.SpecificProxyServer)
        {
            return null;
        }

        if (proxyServerId is null)
        {
            throw new InvalidProxySelectionException(
                "A proxy server must be selected when a specific proxy server is used."
            );
        }

        if (!await proxyServerReadRepository.ExistsAsync(proxyServerId.Value, cancellationToken))
        {
            throw new InvalidProxySelectionException(
                $"The selected proxy server with id {proxyServerId} does not exist."
            );
        }

        return proxyServerId;
    }
}
