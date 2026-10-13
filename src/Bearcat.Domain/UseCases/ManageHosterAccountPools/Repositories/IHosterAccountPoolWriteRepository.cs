using Bearcat.Domain.Entities;

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

    void Add(HosterAccountPool hosterAccountPool);

    void Remove(HosterAccountPool hosterAccountPool);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
