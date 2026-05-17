using Bearcat.Domain.UseCases.ManageHosters.Dto;

namespace Bearcat.Domain.UseCases.ManageHosters.Repositories;

public interface IHosterConfigurationReadRepository
{
    Task<IReadOnlyList<HosterRegistrationDto>> GetAllRegistrationsAsync(
        CancellationToken cancellationToken = default
    );
}
