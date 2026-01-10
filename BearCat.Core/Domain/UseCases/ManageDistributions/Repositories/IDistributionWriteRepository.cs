using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;

public interface IDistributionWriteRepository
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
    void Add(UploadConfig distribution);
    Task<UploadConfig> GetByIdAsync(int id, CancellationToken cancellationToken);
    void Remove(UploadConfig distribution);
}
