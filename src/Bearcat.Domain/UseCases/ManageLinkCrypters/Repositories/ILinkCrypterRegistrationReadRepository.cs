using Bearcat.Domain.UseCases.ManageLinkCrypters.Dto;

namespace Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;

public interface ILinkCrypterRegistrationReadRepository
{
    Task<IReadOnlyList<LinkCrypterRegistrationDto>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<LinkCrypterRegistrationDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );
}
