using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Entities;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UnreadableSecretsRepository(IBearcatWriteDbContext dbWrite)
    : IUnreadableSecretsRepository
{
    public async Task<List<IEntityWithEncryptedSecrets>> GetEntitiesWithEncryptedSecretsAsync(
        CancellationToken cancellationToken
    )
    {
        var entities = new List<IEntityWithEncryptedSecrets>();

        entities.AddRange(
            await dbWrite
                .HosterRegistrations.OrderBy(registration => registration.Name)
                .ToListAsync(cancellationToken)
        );
        entities.AddRange(
            await dbWrite
                .LinkCrypterRegistrations.OrderBy(registration => registration.Name)
                .ToListAsync(cancellationToken)
        );
        entities.AddRange(
            await dbWrite
                .ImageHosterRegistrations.OrderBy(registration => registration.Name)
                .ToListAsync(cancellationToken)
        );
        entities.AddRange(
            await dbWrite
                .DistributionSiteRegistrations.OrderBy(registration => registration.Name)
                .ToListAsync(cancellationToken)
        );
        entities.AddRange(
            await dbWrite
                .RemoteSourceRegistrations.OrderBy(registration => registration.Name)
                .ToListAsync(cancellationToken)
        );
        entities.AddRange(
            await dbWrite
                .NfoDatabaseRegistrations.OrderBy(registration => registration.NfoDatabaseClassName)
                .ToListAsync(cancellationToken)
        );
        entities.AddRange(
            await dbWrite
                .MediaDatabaseRegistrations.OrderBy(registration =>
                    registration.MediaDatabaseClassName
                )
                .ToListAsync(cancellationToken)
        );
        entities.AddRange(await dbWrite.TelegramConfigurations.ToListAsync(cancellationToken));
        entities.AddRange(
            await dbWrite
                .ProxyServers.Where(proxyServer => proxyServer.EncryptedPassword != null)
                .OrderBy(proxyServer => proxyServer.Name)
                .ToListAsync(cancellationToken)
        );

        return entities;
    }

    public async Task<
        List<DistributionSiteRegistration>
    > GetDistributionSiteRegistrationsWithEncryptedSessionAsync(CancellationToken cancellationToken)
    {
        return await dbWrite
            .DistributionSiteRegistrations.Where(registration =>
                registration.EncryptedSession != null
            )
            .OrderBy(registration => registration.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnyUnresolvedUnreadableSecretsNotificationAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.Notifications.AnyAsync(
            notification =>
                notification.NotificationKind == NotificationKind.UnreadableSecretsDetected
                && notification.ResolvedAt == null,
            cancellationToken
        );
    }

    public async Task<bool> AnyUnreadableSecretsAsync(CancellationToken cancellationToken)
    {
        return await dbWrite
            .HosterRegistrations.Where(registration => registration.HasUnreadableSecrets)
            .Select(registration => 1)
            .Concat(
                dbWrite
                    .LinkCrypterRegistrations.Where(registration =>
                        registration.HasUnreadableSecrets
                    )
                    .Select(registration => 1)
            )
            .Concat(
                dbWrite
                    .ImageHosterRegistrations.Where(registration =>
                        registration.HasUnreadableSecrets
                    )
                    .Select(registration => 1)
            )
            .Concat(
                dbWrite
                    .DistributionSiteRegistrations.Where(registration =>
                        registration.HasUnreadableSecrets
                    )
                    .Select(registration => 1)
            )
            .Concat(
                dbWrite
                    .RemoteSourceRegistrations.Where(registration =>
                        registration.HasUnreadableSecrets
                    )
                    .Select(registration => 1)
            )
            .Concat(
                dbWrite
                    .NfoDatabaseRegistrations.Where(registration =>
                        registration.HasUnreadableSecrets
                    )
                    .Select(registration => 1)
            )
            .Concat(
                dbWrite
                    .MediaDatabaseRegistrations.Where(registration =>
                        registration.HasUnreadableSecrets
                    )
                    .Select(registration => 1)
            )
            .Concat(
                dbWrite
                    .TelegramConfigurations.Where(configuration =>
                        configuration.HasUnreadableSecrets
                    )
                    .Select(configuration => 1)
            )
            .Concat(
                dbWrite
                    .ProxyServers.Where(proxyServer => proxyServer.HasUnreadableSecrets)
                    .Select(proxyServer => 1)
            )
            .AnyAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
