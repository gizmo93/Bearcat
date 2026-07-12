using Bearcat.Domain.UseCases.ManageNotifications.Telegram;

namespace Bearcat.Domain.UseCases.ManageNotifications.Repositories;

public interface ITelegramNotificationReadRepository
{
    Task<TelegramConfigurationState?> GetConfigurationStateAsync(
        CancellationToken cancellationToken
    );

    Task<int> GetLatestNotificationIdAsync(CancellationToken cancellationToken);

    Task<List<int>> GetForwardableNotificationIdsAsync(
        int afterId,
        bool forwardInfo,
        bool forwardWarning,
        bool forwardError,
        int take,
        CancellationToken cancellationToken
    );

    Task<int?> GetFirstUnhandledNotificationIdAsync(
        int afterId,
        CancellationToken cancellationToken
    );

    Task<TelegramDeliveryStatus> GetDeliveryStatusAsync(
        int maxAttempts,
        CancellationToken cancellationToken
    );
}
