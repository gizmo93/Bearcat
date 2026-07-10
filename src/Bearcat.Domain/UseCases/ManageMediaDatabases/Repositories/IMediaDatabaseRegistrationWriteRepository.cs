using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageMediaDatabases.Repositories;

public interface IMediaDatabaseRegistrationWriteRepository
{
    Task<MediaDatabaseRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsForClassNameAsync(
        string className,
        int? excludingId = null,
        CancellationToken cancellationToken = default
    );

    void Add(MediaDatabaseRegistration registration);

    void Remove(MediaDatabaseRegistration registration);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
