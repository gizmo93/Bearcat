using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class TelegramConfigurationRepository(IBearcatWriteDbContext dbWrite)
    : ITelegramConfigurationRepository
{
    public async Task<TelegramConfiguration?> GetAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.TelegramConfigurations.SingleOrDefaultAsync(cancellationToken);
    }

    public void Add(TelegramConfiguration configuration)
    {
        dbWrite.TelegramConfigurations.Add(configuration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
