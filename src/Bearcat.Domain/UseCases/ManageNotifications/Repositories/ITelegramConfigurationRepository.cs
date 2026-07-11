using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageNotifications.Repositories;

public interface ITelegramConfigurationRepository
{
    Task<TelegramConfiguration?> GetAsync(CancellationToken cancellationToken);

    void Add(TelegramConfiguration configuration);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
