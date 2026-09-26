using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets.Repositories;
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
