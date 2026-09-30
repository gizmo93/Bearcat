using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;

public interface IDistributionSessionRepository
{
    Task<DistributionSession?> GetByRegistrationIdAsync(
        int registrationId,
        CancellationToken cancellationToken
    );

    Task SaveAsync(
        int registrationId,
        DistributionSession session,
        CancellationToken cancellationToken
    );

    Task RemoveAsync(int registrationId, CancellationToken cancellationToken);
}
