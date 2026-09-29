using System.Net;
using Bearcat.Abstractions.Proxies;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Infrastructure.Proxies;

public class ProxyRoutingCache(
    IServiceScopeFactory serviceScopeFactory,
    ISecretProtector secretProtector
) : IProxyRoutingCache
{
    private volatile ProxyRoutingTable routingTable = new(
        new Dictionary<Uri, ResolvedProxyServer>(),
        new Dictionary<int, ResolvedProxyServer>(),
        new Dictionary<ProxyCategory, ResolvedProxyServer>()
    );

    public ResolvedProxyServer? GetProxyServerForCategory(ProxyCategory category)
    {
        return routingTable.ProxyServerByCategory.GetValueOrDefault(category);
    }

    public ResolvedProxyServer GetProxyServerById(int proxyServerId)
    {
        return routingTable.ProxyServersById.GetValueOrDefault(proxyServerId)
            ?? throw new InvalidOperationException(
                $"The proxy server with id {proxyServerId} is not known to the proxy routing cache."
            );
    }

    public NetworkCredential? GetCredentialForProxyAddress(Uri proxyUri)
    {
        return routingTable.ProxyServersByUri.GetValueOrDefault(proxyUri)?.Credential;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        var proxyServerRepository =
            scope.ServiceProvider.GetRequiredService<IProxyServerReadRepository>();

        var categoryDefaultRepository =
            scope.ServiceProvider.GetRequiredService<IProxyCategoryDefaultReadRepository>();

        var proxyServers = (await proxyServerRepository.GetAllForRoutingAsync(cancellationToken))
            .Select(Resolve)
            .ToList();

        var proxyServersByUri = proxyServers.ToDictionary(proxyServer => proxyServer.ProxyUri);
        var proxyServersById = proxyServers.ToDictionary(proxyServer => proxyServer.Id);

        var proxyServerByCategory = (await categoryDefaultRepository.GetAllAsync(cancellationToken))
            .Where(categoryDefault => categoryDefault.ProxyServerId is not null)
            .ToDictionary(
                categoryDefault => categoryDefault.ProxyCategory,
                categoryDefault => proxyServersById[categoryDefault.ProxyServerId!.Value]
            );

        routingTable = new ProxyRoutingTable(
            proxyServersByUri,
            proxyServersById,
            proxyServerByCategory
        );
    }

    private ResolvedProxyServer Resolve(ProxyServerRoutingReadModel proxyServer)
    {
        var proxyUri = new UriBuilder(
            GetProxyUriScheme(proxyServer.ProxyType),
            proxyServer.Host,
            proxyServer.Port
        ).Uri;

        if (proxyServer.HasUnreadableSecrets)
        {
            return new ResolvedProxyServer(
                proxyServer.Id,
                proxyServer.Name,
                proxyUri,
                Credential: null,
                HasUnreadableSecrets: true
            );
        }

        var password = proxyServer.EncryptedPassword is null
            ? null
            : secretProtector.Unprotect(proxyServer.EncryptedPassword);

        var credential = proxyServer.Username is null
            ? null
            : new NetworkCredential(proxyServer.Username, password);

        return new ResolvedProxyServer(
            proxyServer.Id,
            proxyServer.Name,
            proxyUri,
            credential,
            HasUnreadableSecrets: false
        );
    }

    private static string GetProxyUriScheme(ProxyType proxyType)
    {
        return proxyType switch
        {
            ProxyType.Http => "http",
            ProxyType.Socks5 => "socks5",
            _ => throw new ArgumentOutOfRangeException(nameof(proxyType), proxyType, null),
        };
    }

    private sealed record ProxyRoutingTable(
        IReadOnlyDictionary<Uri, ResolvedProxyServer> ProxyServersByUri,
        IReadOnlyDictionary<int, ResolvedProxyServer> ProxyServersById,
        IReadOnlyDictionary<ProxyCategory, ResolvedProxyServer> ProxyServerByCategory
    );
}
