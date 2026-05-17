using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageApplicationConfigurations;

public interface IApplicationConfigurationOverrideWriteRepository
{
    Task<ApplicationConfigurationOverride?> GetAsync(
        string configurationKey,
        string propertyName,
        CancellationToken cancellationToken
    );

    void Add(ApplicationConfigurationOverride configurationOverride);

    void Remove(ApplicationConfigurationOverride configurationOverride);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
