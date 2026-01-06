using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;

public interface IDistributionWriteRepository
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
    void Add(Distribution distribution);
    Task<Distribution> GetByIdAsync(int id, CancellationToken cancellationToken);
    void Remove(Distribution distribution);
}
