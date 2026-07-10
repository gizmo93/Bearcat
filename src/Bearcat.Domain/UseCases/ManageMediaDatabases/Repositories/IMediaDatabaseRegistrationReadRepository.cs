using Bearcat.Domain.UseCases.ManageMediaDatabases.ReadModels;

namespace Bearcat.Domain.UseCases.ManageMediaDatabases.Repositories;

public interface IMediaDatabaseRegistrationReadRepository
{
    Task<IReadOnlyList<MediaDatabaseRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<MediaDatabaseRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsForClassNameAsync(
        string className,
        CancellationToken cancellationToken = default
    );
}
