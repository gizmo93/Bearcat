using Bearcat.Domain.UseCases.ManageNfoDatabases.ReadModels;

namespace Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;

public interface INfoDatabaseRegistrationReadRepository
{
    Task<IReadOnlyList<NfoDatabaseRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<NfoDatabaseRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsForClassNameAsync(
        string className,
        CancellationToken cancellationToken = default
    );
}
