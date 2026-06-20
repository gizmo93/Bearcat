using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;

public interface IDistributionSessionStore
{
    Task<DistributionSession?> TryGetAsync(int registrationId, CancellationToken cancellationToken);

    Task SaveAsync(
        int registrationId,
        DistributionSession session,
        CancellationToken cancellationToken
    );

    Task RemoveAsync(int registrationId, CancellationToken cancellationToken);
}
