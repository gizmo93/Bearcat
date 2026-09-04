using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class TelegramNotificationReadRepository(IBearcatReadDbContext dbRead)
    : ITelegramNotificationReadRepository
{
    public async Task<TelegramConfigurationState?> GetConfigurationStateAsync(
        CancellationToken cancellationToken
    )
    {
        var configuration = await dbRead.TelegramConfigurations.SingleOrDefaultAsync(
            cancellationToken
        );

        return configuration is null ? null : TelegramConfigurationState.From(configuration);
    }

    public async Task<int> GetLatestNotificationIdAsync(CancellationToken cancellationToken)
    {
        return await dbRead.Notifications.MaxAsync(
                notification => (int?)notification.Id,
                cancellationToken
            ) ?? 0;
    }

    public async Task<List<int>> GetForwardableNotificationIdsAsync(
        int afterId,
        bool forwardInfo,
        bool forwardWarning,
        bool forwardError,
        int take,
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .Notifications.Where(notification =>
                notification.Id > afterId
                && notification.ResolvedAt == null
                && (
                    (notification.NotificationSeverity == NotificationSeverity.Info && forwardInfo)
                    || (
                        notification.NotificationSeverity == NotificationSeverity.Warning
                        && forwardWarning
                    )
                    || (
                        notification.NotificationSeverity == NotificationSeverity.Error
                        && forwardError
                    )
                )
                && !dbRead.TelegramDeliveries.Any(delivery =>
                    delivery.NotificationId == notification.Id
                )
            )
            .OrderBy(notification => notification.Id)
            .Select(notification => notification.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int?> GetFirstUnhandledNotificationIdAsync(
        int afterId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .Notifications.Where(notification =>
                notification.Id > afterId
                && notification.ResolvedAt == null
                && !dbRead.TelegramDeliveries.Any(delivery =>
                    delivery.NotificationId == notification.Id
                )
            )
            .MinAsync(notification => (int?)notification.Id, cancellationToken);
    }

    public async Task<TelegramDeliveryStatus> GetDeliveryStatusAsync(
        int maxAttempts,
        CancellationToken cancellationToken
    )
    {
        var pendingCount = await dbRead.TelegramDeliveries.CountAsync(
            delivery => delivery.DeliveredAt == null && delivery.AttemptCount < maxAttempts,
            cancellationToken
        );

        var failedCount = await dbRead.TelegramDeliveries.CountAsync(
            delivery => delivery.DeliveredAt == null && delivery.AttemptCount >= maxAttempts,
            cancellationToken
        );

        var lastDeliveredAt = await dbRead
            .TelegramDeliveries.Where(delivery => delivery.DeliveredAt != null)
            .MaxAsync(delivery => delivery.DeliveredAt, cancellationToken);

        var lastError = await dbRead
            .TelegramDeliveries.Where(delivery =>
                delivery.DeliveredAt == null && delivery.LastError != null
            )
            .OrderByDescending(delivery => delivery.Id)
            .Select(delivery => delivery.LastError)
            .FirstOrDefaultAsync(cancellationToken);

        return new TelegramDeliveryStatus(pendingCount, failedCount, lastDeliveredAt, lastError);
    }
}
