using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class TelegramDeliveryRepository(IBearcatWriteDbContext dbWrite)
    : ITelegramDeliveryRepository
{
    public async Task<List<TelegramDelivery>> GetPendingAsync(
        DateTime now,
        int maxAttempts,
        int take,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .TelegramDeliveries.Include(delivery => delivery.Notification)
            .Where(delivery =>
                delivery.DeliveredAt == null
                && delivery.AttemptCount < maxAttempts
                && (delivery.NextAttemptAt == null || delivery.NextAttemptAt <= now)
            )
            .OrderBy(delivery => delivery.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public void Add(TelegramDelivery delivery)
    {
        dbWrite.TelegramDeliveries.Add(delivery);
    }

    public async Task DeletePendingAsync(CancellationToken cancellationToken)
    {
        await dbWrite
            .TelegramDeliveries.Where(delivery => delivery.DeliveredAt == null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
