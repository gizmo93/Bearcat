using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageNotifications.Repositories;

public interface ITelegramDeliveryRepository
{
    Task<List<TelegramDelivery>> GetPendingAsync(
        DateTime now,
        int maxAttempts,
        int take,
        CancellationToken cancellationToken
    );

    void Add(TelegramDelivery delivery);

    Task DeletePendingAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
