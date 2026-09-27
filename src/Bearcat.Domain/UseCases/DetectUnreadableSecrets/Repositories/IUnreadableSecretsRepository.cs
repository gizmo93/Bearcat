using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Entities;

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

    Task<bool> AnyUnresolvedUnreadableSecretsNotificationAsync(CancellationToken cancellationToken);

    Task<bool> AnyUnreadableSecretsAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
