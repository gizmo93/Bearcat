using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageQualityProfiles.Repositories;

public interface IQualityProfileWriteRepository
{
    Task<QualityProfile> GetByIdAsync(int qualityProfileId, CancellationToken cancellationToken);

    void Add(QualityProfile qualityProfile);

    void Remove(QualityProfile qualityProfile);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
