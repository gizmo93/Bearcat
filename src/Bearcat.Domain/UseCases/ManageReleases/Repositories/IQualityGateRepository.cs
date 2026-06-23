using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IQualityGateRepository
{
    Task<Release?> GetForEvaluationAsync(int releaseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Release>> GetPendingReleasesAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
