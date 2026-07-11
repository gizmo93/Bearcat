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
                    (notification.NotificationType == NotificationType.Info && forwardInfo)
                    || (notification.NotificationType == NotificationType.Warning && forwardWarning)
                    || (notification.NotificationType == NotificationType.Error && forwardError)
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
}
