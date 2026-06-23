using Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;

namespace Bearcat.Domain.UseCases.ManageQualityProfiles.Repositories;

public interface IQualityProfileReadRepository
{
    Task<IReadOnlyList<QualityProfileReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<QualityProfileDetailReadModel?> GetDetailAsync(
        int qualityProfileId,
        CancellationToken cancellationToken = default
    );
}
