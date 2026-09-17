using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseFolderRetirementRepository
{
    Task<IReadOnlyList<Release>> GetConversionCandidatesAsync(
        DateTime cutoff,
        CancellationToken cancellationToken
    );

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
