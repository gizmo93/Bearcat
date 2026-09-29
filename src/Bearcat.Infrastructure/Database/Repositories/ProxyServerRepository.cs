using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ProxyServerRepository(IBearcatReadDbContext dbRead, IBearcatWriteDbContext dbWrite)
    : IProxyServerReadRepository,
        IProxyServerWriteRepository
{
    public async Task<IReadOnlyList<ProxyServerReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ProxyServers.OrderBy(proxyServer => proxyServer.Name)
            .Select(proxyServer => new ProxyServerReadModel(
                proxyServer.Id,
                proxyServer.Name,
                proxyServer.ProxyType,
                proxyServer.Host,
                proxyServer.Port,
                proxyServer.Username,
                proxyServer.EncryptedPassword != null,
                proxyServer.HasUnreadableSecrets
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProxyServerReadModel?> GetReadModelAsync(
        int proxyServerId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ProxyServers.Where(proxyServer => proxyServer.Id == proxyServerId)
            .Select(proxyServer => new ProxyServerReadModel(
                proxyServer.Id,
                proxyServer.Name,
                proxyServer.ProxyType,
                proxyServer.Host,
                proxyServer.Port,
                proxyServer.Username,
                proxyServer.EncryptedPassword != null,
                proxyServer.HasUnreadableSecrets
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProxyServerRoutingReadModel>> GetAllForRoutingAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .ProxyServers.OrderBy(proxyServer => proxyServer.Id)
            .Select(proxyServer => new ProxyServerRoutingReadModel(
                proxyServer.Id,
                proxyServer.Name,
                proxyServer.ProxyType,
                proxyServer.Host,
                proxyServer.Port,
                proxyServer.Username,
                proxyServer.EncryptedPassword,
                proxyServer.HasUnreadableSecrets
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProxyServer> GetByIdAsync(
        int proxyServerId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.ProxyServers.FirstAsync(
            proxyServer => proxyServer.Id == proxyServerId,
            cancellationToken
        );
    }

    public async Task<bool> NameExistsAsync(
        string name,
        int? excludedProxyServerId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.ProxyServers.AnyAsync(
            proxyServer => proxyServer.Name == name && proxyServer.Id != excludedProxyServerId,
            cancellationToken
        );
    }

    public async Task<bool> HostAndPortExistAsync(
        string host,
        int port,
        int? excludedProxyServerId,
        CancellationToken cancellationToken
    )
    {
        var lowerCaseHost = host.ToLowerInvariant();

        return await dbRead.ProxyServers.AnyAsync(
            proxyServer =>
                proxyServer.Host.ToLower() == lowerCaseHost
                && proxyServer.Port == port
                && proxyServer.Id != excludedProxyServerId,
            cancellationToken
        );
    }

    public async Task<bool> ExistsAsync(int proxyServerId, CancellationToken cancellationToken)
    {
        return await dbRead.ProxyServers.AnyAsync(
            proxyServer => proxyServer.Id == proxyServerId,
            cancellationToken
        );
    }

    public async Task<ProxyServerUsageReadModel> GetUsageAsync(
        int proxyServerId,
        CancellationToken cancellationToken
    )
    {
        var categoryDefaults = await dbRead
            .ProxyCategoryDefaults.Where(proxyCategoryDefault =>
                proxyCategoryDefault.ProxyServerId == proxyServerId
            )
            .Select(proxyCategoryDefault => proxyCategoryDefault.ProxyCategory)
            .OrderBy(proxyCategory => proxyCategory)
            .ToListAsync(cancellationToken);

        var registrations = await dbRead
            .HosterRegistrations.Where(registration =>
                registration.UploadProxyServerId == proxyServerId
                || registration.MirrorDownloadProxyServerId == proxyServerId
            )
            .Select(registration => new
            {
                RegistrationType = ProxyUsingRegistrationType.Hoster,
                RegistrationName = registration.Name,
            })
            .Concat(
                dbRead
                    .ImageHosterRegistrations.Where(registration =>
                        registration.ProxyServerId == proxyServerId
                    )
                    .Select(registration => new
                    {
                        RegistrationType = ProxyUsingRegistrationType.ImageHoster,
                        RegistrationName = registration.Name,
                    })
            )
            .Concat(
                dbRead
                    .LinkCrypterRegistrations.Where(registration =>
                        registration.ProxyServerId == proxyServerId
                    )
                    .Select(registration => new
                    {
                        RegistrationType = ProxyUsingRegistrationType.LinkCrypter,
                        RegistrationName = registration.Name,
                    })
            )
            .ToListAsync(cancellationToken);

        return new ProxyServerUsageReadModel(
            categoryDefaults,
            registrations
                .OrderBy(registration => registration.RegistrationType)
                .ThenBy(registration => registration.RegistrationName)
                .Select(registration => new ProxyServerRegistrationUsageReadModel(
                    registration.RegistrationType,
                    registration.RegistrationName
                ))
                .ToList()
        );
    }

    public void Add(ProxyServer proxyServer)
    {
        dbWrite.Add(proxyServer);
    }

    public void Remove(ProxyServer proxyServer)
    {
        dbWrite.Remove(proxyServer);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
