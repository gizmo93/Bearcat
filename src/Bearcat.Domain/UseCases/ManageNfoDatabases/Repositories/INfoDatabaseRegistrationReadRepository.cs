using Bearcat.Domain.UseCases.ManageNfoDatabases.Dto;

namespace Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;

public interface INfoDatabaseRegistrationReadRepository
{
    Task<IReadOnlyList<NfoDatabaseRegistrationDto>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<NfoDatabaseRegistrationDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsForClassNameAsync(
        string className,
        CancellationToken cancellationToken = default
    );
}
