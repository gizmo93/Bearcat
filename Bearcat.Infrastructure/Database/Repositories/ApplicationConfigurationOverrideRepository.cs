using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ApplicationConfigurationOverrideRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
)
    : IApplicationConfigurationOverrideReadRepository,
        IApplicationConfigurationOverrideWriteRepository
{
    public async Task<IReadOnlyList<ApplicationConfigurationOverride>> GetAllAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .ApplicationConfigurationOverrides.OrderBy(c => c.ConfigurationKey)
            .ThenBy(c => c.PropertyName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApplicationConfigurationOverride?> GetAsync(
        string configurationKey,
        string propertyName,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.ApplicationConfigurationOverrides.FirstOrDefaultAsync(
            c => c.ConfigurationKey == configurationKey && c.PropertyName == propertyName,
            cancellationToken
        );
    }

    public void Add(ApplicationConfigurationOverride configurationOverride)
    {
        dbWrite.Add(configurationOverride);
    }

    public void Remove(ApplicationConfigurationOverride configurationOverride)
    {
        dbWrite.Remove(configurationOverride);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
