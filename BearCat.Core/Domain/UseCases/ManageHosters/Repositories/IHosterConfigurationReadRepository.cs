using BearCat.Core.Domain.UseCases.ManageHosters.Dto;

namespace BearCat.Core.Domain.UseCases.ManageHosters.Repositories;

public interface IHosterConfigurationReadRepository
{
    Task<IReadOnlyList<HosterRegistrationDto>> GetAllRegistrationsAsync(
        CancellationToken cancellationToken = default);
}
