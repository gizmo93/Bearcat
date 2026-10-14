using System.Globalization;
using System.Text;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed class TelegramDeliveryService(
    TelegramConfigurationService configurationService,
    ITelegramNotificationReadRepository readRepository,
    INotificationReadRepository notificationReadRepository,
    ITelegramDeliveryRepository deliveryRepository,
    ISecretProtector secretProtector,
    ITelegramClient telegramClient,
    TimeProvider timeProvider
)
{
    private const int QueueBatchSize = 100;
    private const int SendBatchSize = 10;
    private const int MaxDeliveryAttempts = 10;
    private const int MaxLastErrorLength = 2000;
    private const int FirstRetryDelaySeconds = 5;
    private const int MaxRetryDelaySeconds = 300;

    public async Task ProcessDeliveriesAsync(CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCachedStateWithReadableBotTokenAsync(
            cancellationToken
        );

        if (configuration?.ChatId is null)
        {
            return;
        }

        await QueueDeliveriesForNewNotificationsAsync(configuration, cancellationToken);

        var deliveries = await deliveryRepository.GetPendingAsync(
            timeProvider.GetLocalNow(),
            MaxDeliveryAttempts,
            SendBatchSize,
            cancellationToken
        );

        await SendDeliveriesAsync(configuration, deliveries, cancellationToken);
    }

    public async Task SendTestMessageAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.GetCachedStateAsync(cancellationToken);

        if (configuration?.ChatId is null)
        {
            throw new InvalidOperationException("Telegram is not connected.");
        }

        await telegramClient.SendMessageAsync(
            secretProtector.Unprotect(configuration.EncryptedBotToken),
            configuration.ChatId.Value,
            "Bearcat Telegram notifications are connected.",
            cancellationToken
        );
    }

    public async Task<TelegramDeliveryStatus> GetDeliveryStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await readRepository.GetDeliveryStatusAsync(MaxDeliveryAttempts, cancellationToken);
    }

    private async Task QueueDeliveriesForNewNotificationsAsync(
        TelegramConfigurationState configuration,
        CancellationToken cancellationToken
    )
    {
        var notificationIds = await readRepository.GetForwardableNotificationIdsAsync(
            configuration.ForwardNotificationsAfterId,
            configuration.ForwardInfo,
            configuration.ForwardWarning,
            configuration.ForwardError,
            QueueBatchSize,
            cancellationToken
        );

        foreach (var notificationId in notificationIds)
        {
            deliveryRepository.Add(
                new TelegramDelivery
                {
                    NotificationId = notificationId,
                    CreatedAt = timeProvider.GetLocalNow(),
                }
            );
        }

        if (notificationIds.Count > 0)
        {
            await deliveryRepository.SaveChangesAsync(cancellationToken);
        }

        await MoveForwardNotificationsAfterIdPastHandledNotificationsAsync(
            configuration.ForwardNotificationsAfterId,
            cancellationToken
        );
    }

    private async Task MoveForwardNotificationsAfterIdPastHandledNotificationsAsync(
        int currentForwardNotificationsAfterId,
        CancellationToken cancellationToken
    )
    {
        var firstUnhandledNotificationId =
            await readRepository.GetFirstUnhandledNotificationIdAsync(
                currentForwardNotificationsAfterId,
                cancellationToken
            );

        var newForwardNotificationsAfterId = firstUnhandledNotificationId is null
            ? await readRepository.GetLatestNotificationIdAsync(cancellationToken)
            : firstUnhandledNotificationId.Value - 1;

        if (newForwardNotificationsAfterId <= currentForwardNotificationsAfterId)
        {
            return;
        }

        var configuration = await configurationService.GetConfigurationAsync(cancellationToken);
        configuration.ForwardNotificationsAfterId = newForwardNotificationsAfterId;
        await configurationService.SaveChangesAndInvalidateCacheAsync(cancellationToken);
    }

    private async Task SendDeliveriesAsync(
        TelegramConfigurationState configuration,
        List<TelegramDelivery> deliveries,
        CancellationToken cancellationToken
    )
    {
        var botToken = secretProtector.Unprotect(configuration.EncryptedBotToken);
        var relatedEntities = await GetRelatedEntitiesAsync(deliveries, cancellationToken);

        foreach (var delivery in deliveries)
        {
            try
            {
                relatedEntities.TryGetValue(delivery.NotificationId, out var relatedEntity);
                await telegramClient.SendMessageAsync(
                    botToken,
                    configuration.ChatId!.Value,
                    CreateMessage(configuration, delivery.Notification, relatedEntity),
                    cancellationToken
                );
                delivery.DeliveredAt = timeProvider.GetLocalNow();
                delivery.LastError = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ScheduleRetry(delivery, exception);
            }

            await deliveryRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private void ScheduleRetry(TelegramDelivery delivery, Exception exception)
    {
        delivery.AttemptCount++;
        delivery.LastError = exception.Message[
            ..Math.Min(exception.Message.Length, MaxLastErrorLength)
        ];
        delivery.NextAttemptAt = timeProvider
            .GetLocalNow()
            .AddSeconds(
                Math.Min(
                    MaxRetryDelaySeconds,
                    FirstRetryDelaySeconds * Math.Pow(2, delivery.AttemptCount - 1)
                )
            );
    }

    private async Task<Dictionary<int, NotificationRelatedEntityReadModel>> GetRelatedEntitiesAsync(
        List<TelegramDelivery> deliveries,
        CancellationToken cancellationToken
    )
    {
        if (deliveries.Count == 0)
        {
            return [];
        }

        var readModels = await notificationReadRepository.GetByIdsAsync(
            deliveries.Select(delivery => delivery.NotificationId).ToList(),
            cancellationToken
        );

        return readModels
            .Where(readModel => readModel.RelatedEntity is not null)
            .ToDictionary(
                readModel => readModel.NotificationId,
                readModel => readModel.RelatedEntity!
            );
    }

    private static string CreateMessage(
        TelegramConfigurationState configuration,
        Notification notification,
        NotificationRelatedEntityReadModel? relatedEntity
    )
    {
        var icon = notification.NotificationSeverity switch
        {
            NotificationSeverity.Info => "ℹ️",
            NotificationSeverity.Warning => "⚠️",
            NotificationSeverity.Error => "🔴",
            _ => "🔔",
        };
        var url = $"{configuration.NotificationBaseUrl}/notifications/{notification.Id}";

        var builder = new StringBuilder();
        builder.Append(
            CultureInfo.InvariantCulture,
            $"{icon} Bearcat: {notification.NotificationSeverity}\n\n{notification.Message}"
        );

        if (relatedEntity is not null)
        {
            builder.Append(
                CultureInfo.InvariantCulture,
                $"\n\n{DescribeEntityType(relatedEntity.EntityType)}: {relatedEntity.DisplayName}"
            );
        }

        builder.Append(CultureInfo.InvariantCulture, $"\n\n{url}");
        return builder.ToString();
    }

    private static string DescribeEntityType(string entityType)
    {
        return entityType switch
        {
            "Upload" => "Upload",
            "Archive" => "Archive",
            "LinkCrypterContainer" => "Link container",
            "HosterRegistration" => "Hoster registration",
            "Release" => "Release",
            _ => entityType,
        };
    }
}
