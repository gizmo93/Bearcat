using Bearcat.Domain.UseCases.ManageHosters.ReadModels;

namespace Bearcat.Domain.UseCases.ManageHosters.Repositories;

public interface IHosterConfigurationReadRepository
{
    Task<IReadOnlyList<HosterRegistrationReadModel>> GetAllRegistrationsAsync(
        CancellationToken cancellationToken = default
    );
}
