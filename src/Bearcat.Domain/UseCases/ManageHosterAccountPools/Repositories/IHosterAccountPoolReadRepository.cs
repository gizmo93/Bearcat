using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;

namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.Repositories;

public interface IHosterAccountPoolReadRepository
{
    Task<IReadOnlyList<HosterAccountPoolReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<HosterAccountPoolReadModel?> GetDetailAsync(
        int hosterAccountPoolId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<HosterAccountPoolCandidateReadModel>> GetCandidateRegistrationsAsync(
        string hosterClassName,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<string>> GetHosterClassNamesWithRegistrationsAsync(
        CancellationToken cancellationToken = default
    );
}
