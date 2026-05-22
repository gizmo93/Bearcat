using Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;

namespace Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;

public interface ILinkCrypterRegistrationReadRepository
{
    Task<IReadOnlyList<LinkCrypterRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<LinkCrypterRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );
}
