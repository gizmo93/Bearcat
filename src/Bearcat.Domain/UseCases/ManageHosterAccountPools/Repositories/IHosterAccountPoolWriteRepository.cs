using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Deletion;

namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.Repositories;

public interface IHosterAccountPoolWriteRepository
{
    Task<HosterAccountPool> GetByIdAsync(
        int hosterAccountPoolId,
        CancellationToken cancellationToken
    );

    Task<bool> NameExistsAsync(
        string name,
        int? excludedHosterAccountPoolId,
        CancellationToken cancellationToken
    );

    Task<Dictionary<int, string>> GetHosterClassNamesByHosterRegistrationIdAsync(
        IReadOnlyList<int> hosterRegistrationIds,
        CancellationToken cancellationToken
    );

    Task<HosterAccountPoolUsage> GetUsageAsync(
        int hosterAccountPoolId,
        CancellationToken cancellationToken
    );

    void Add(HosterAccountPool hosterAccountPool);

    void Remove(HosterAccountPool hosterAccountPool);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
