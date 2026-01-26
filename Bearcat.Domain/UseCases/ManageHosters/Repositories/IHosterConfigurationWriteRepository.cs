using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageHosters.Repositories;

public interface IHosterConfigurationWriteRepository
{
    Task<HosterRegistration> GetByIdAsync(int id, CancellationToken cancellationToken);

    void Add(HosterRegistration registration);

    void Remove(HosterRegistration registration);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
