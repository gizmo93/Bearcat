using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageSeriesDatabases.Repositories;

public interface ISeriesDatabaseRegistrationWriteRepository
{
    Task<SeriesDatabaseRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsForClassNameAsync(
        string className,
        int? excludingId = null,
        CancellationToken cancellationToken = default
    );

    void Add(SeriesDatabaseRegistration registration);

    void Remove(SeriesDatabaseRegistration registration);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
