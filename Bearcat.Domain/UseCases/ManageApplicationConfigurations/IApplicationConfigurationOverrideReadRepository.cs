using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageApplicationConfigurations;

public interface IApplicationConfigurationOverrideReadRepository
{
    Task<IReadOnlyList<ApplicationConfigurationOverride>> GetAllAsync(
        CancellationToken cancellationToken
    );
}
