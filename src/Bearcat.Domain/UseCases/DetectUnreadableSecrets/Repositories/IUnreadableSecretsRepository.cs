using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.DetectUnreadableSecrets.Repositories;

public interface IUnreadableSecretsRepository
{
    Task<List<IEntityWithEncryptedSecrets>> GetEntitiesWithEncryptedSecretsAsync(
        CancellationToken cancellationToken
    );

    Task<
        List<DistributionSiteRegistration>
    > GetDistributionSiteRegistrationsWithEncryptedSessionAsync(
        CancellationToken cancellationToken
    );

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
