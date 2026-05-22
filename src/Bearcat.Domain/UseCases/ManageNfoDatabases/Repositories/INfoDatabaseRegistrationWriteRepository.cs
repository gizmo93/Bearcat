using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;

public interface INfoDatabaseRegistrationWriteRepository
{
    Task<NfoDatabaseRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsForClassNameAsync(
        string className,
        int? excludingId = null,
        CancellationToken cancellationToken = default
    );

    void Add(NfoDatabaseRegistration registration);

    void Remove(NfoDatabaseRegistration registration);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
