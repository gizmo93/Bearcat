using Bearcat.Domain.UseCases.ManageSeriesDatabases.ReadModels;

namespace Bearcat.Domain.UseCases.ManageSeriesDatabases.Repositories;

public interface ISeriesDatabaseRegistrationReadRepository
{
    Task<IReadOnlyList<SeriesDatabaseRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<SeriesDatabaseRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsForClassNameAsync(
        string className,
        CancellationToken cancellationToken = default
    );
}
