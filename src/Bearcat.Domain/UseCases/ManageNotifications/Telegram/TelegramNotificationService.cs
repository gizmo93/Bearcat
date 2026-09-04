using System.Security.Cryptography;
using System.Text;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed class TelegramNotificationService(
    ITelegramConfigurationRepository configurationRepository,
    ITelegramNotificationReadRepository readRepository,
    INotificationReadRepository notificationReadRepository,
    ITelegramDeliveryRepository deliveryRepository,
    ISecretProtector secretProtector,
    ITelegramClient telegramClient,
    TimeProvider timeProvider,
    TelegramConfigurationCache configurationCache
)
{
    private static readonly TimeSpan PairingDuration = TimeSpan.FromMinutes(10);
    private const int PrepareBatchSize = 100;
    private const int SendBatchSize = 10;
    private const int MaxDeliveryAttempts = 10;

    public async Task<TelegramSettings> GetSettingsAsync(
        string defaultBaseUrl,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCacheInitializedAsync(cancellationToken);

        var configuration = configurationCache.Current;

        if (configuration is null)
        {
            return new TelegramSettings(
                IsConfigured: false,
                BotUsername: null,
                NotificationBaseUrl: defaultBaseUrl.TrimEnd('/'),
                IsConnected: false,
                ChatName: null,
                ForwardInfo: true,
                ForwardWarning: true,
                ForwardError: true,
                IsPairing: false
            );
        }

        return new TelegramSettings(
            IsConfigured: true,
            BotUsername: configuration.BotUsername,
            NotificationBaseUrl: configuration.NotificationBaseUrl,
            IsConnected: configuration.ChatId.HasValue,
            ChatName: configuration.ChatName,
            ForwardInfo: configuration.ForwardInfo,
            ForwardWarning: configuration.ForwardWarning,
            ForwardError: configuration.ForwardError,
            IsPairing: configuration.PairingExpiresAt > timeProvider.GetLocalNow()
        );
    }

    public async Task SaveConfigurationAsync(
        string? botToken,
        string notificationBaseUrl,
        CancellationToken cancellationToken = default
    )
    {
        var baseUrl = NormalizeBaseUrl(notificationBaseUrl);

        var configuration = await configurationRepository.GetAsync(cancellationToken);

        if (configuration is null)
        {
            configuration = await CreateConfigurationAsync(botToken, baseUrl, cancellationToken);
            configurationRepository.Add(configuration);
        }
        else
        {
            await UpdateConfigurationAsync(configuration, botToken, baseUrl, cancellationToken);
        }

        await configurationRepository.SaveChangesAsync(cancellationToken);
        configurationCache.Invalidate();
    }

    public async Task SaveLevelsAsync(
        bool forwardInfo,
        bool forwardWarning,
        bool forwardError,
        CancellationToken cancellationToken = default
    )
    {
        var configuration = await GetConfigurationAsync(cancellationToken);

        configuration.ForwardInfo = forwardInfo;
        configuration.ForwardWarning = forwardWarning;
        configuration.ForwardError = forwardError;

        await configurationRepository.SaveChangesAsync(cancellationToken);
        configurationCache.Invalidate();
    }

    public async Task<string> BeginPairingAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        var token = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        configuration.PairingTokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))
        );

        configuration.PairingExpiresAt = timeProvider.GetLocalNow().Add(PairingDuration);
        await configurationRepository.SaveChangesAsync(cancellationToken);

        configurationCache.Invalidate();

        return $"https://t.me/{configuration.BotUsername}?start={token}";
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);

        configuration.ChatId = null;
        configuration.ChatName = null;
        configuration.PairingTokenHash = null;
        configuration.PairingExpiresAt = null;
        configuration.ForwardNotificationsAfterId = await GetLatestNotificationIdAsync(
            cancellationToken
        );

        await deliveryRepository.DeletePendingAsync(cancellationToken);
        await configurationRepository.SaveChangesAsync(cancellationToken);

        configurationCache.Invalidate();
    }

    public async Task SendTestMessageAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCacheInitializedAsync(cancellationToken);

        var configuration = configurationCache.Current;

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

    public bool HasPendingPairing => configurationCache.Current?.PairingTokenHash is not null;

    public async Task PollPairingAsync(CancellationToken cancellationToken)
    {
        await EnsureCacheInitializedAsync(cancellationToken);

        var pairing = configurationCache.Current;
        if (pairing?.PairingTokenHash is null || pairing.PairingExpiresAt is null)
        {
            return;
        }

        if (pairing.PairingExpiresAt <= timeProvider.GetLocalNow())
        {
            await ClearExpiredPairingAsync(cancellationToken);
            return;
        }

        var updates = await ReceivePairingUpdatesAsync(pairing, cancellationToken);
        if (updates.Count > 0)
        {
            await ApplyPairingUpdatesAsync(updates, cancellationToken);
        }
    }

    public async Task ProcessDeliveriesAsync(CancellationToken cancellationToken)
    {
        await EnsureCacheInitializedAsync(cancellationToken);

        var configuration = configurationCache.Current;

        if (configuration?.ChatId is null)
        {
            return;
        }

        await PrepareDeliveriesAsync(configuration, cancellationToken);

        var deliveries = await deliveryRepository.GetPendingAsync(
            timeProvider.GetLocalNow(),
            MaxDeliveryAttempts,
            SendBatchSize,
            cancellationToken
        );

        await SendDeliveriesAsync(configuration, deliveries, cancellationToken);
    }

    private async Task<TelegramConfiguration> CreateConfigurationAsync(
        string? botToken,
        string baseUrl,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException("A Telegram bot token is required.");
        }

        var bot = await telegramClient.GetBotAsync(botToken, cancellationToken);
        return new TelegramConfiguration
        {
            EncryptedBotToken = secretProtector.Protect(botToken),
            BotUsername = bot.Username,
            NotificationBaseUrl = baseUrl,
            ForwardNotificationsAfterId = await GetLatestNotificationIdAsync(cancellationToken),
        };
    }

    private async Task UpdateConfigurationAsync(
        TelegramConfiguration configuration,
        string? botToken,
        string baseUrl,
        CancellationToken cancellationToken
    )
    {
        configuration.NotificationBaseUrl = baseUrl;

        if (string.IsNullOrWhiteSpace(botToken))
        {
            return;
        }

        var bot = await telegramClient.GetBotAsync(botToken, cancellationToken);

        configuration.EncryptedBotToken = secretProtector.Protect(botToken);
        configuration.BotUsername = bot.Username;
        configuration.ChatId = null;
        configuration.ChatName = null;
        configuration.PairingTokenHash = null;
        configuration.PairingExpiresAt = null;
        configuration.UpdateOffset = 0;
        configuration.ForwardNotificationsAfterId = await GetLatestNotificationIdAsync(
            cancellationToken
        );
        await deliveryRepository.DeletePendingAsync(cancellationToken);
    }

    private async Task ClearExpiredPairingAsync(CancellationToken cancellationToken)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);

        configuration.PairingTokenHash = null;
        configuration.PairingExpiresAt = null;

        await configurationRepository.SaveChangesAsync(cancellationToken);
        configurationCache.Invalidate();
    }

    private async Task<IReadOnlyList<TelegramUpdate>> ReceivePairingUpdatesAsync(
        TelegramConfigurationState pairing,
        CancellationToken cancellationToken
    )
    {
        return await telegramClient.GetUpdatesAsync(
            secretProtector.Unprotect(pairing.EncryptedBotToken),
            (int)pairing.UpdateOffset,
            cancellationToken
        );
    }

    private async Task ApplyPairingUpdatesAsync(
        IReadOnlyList<TelegramUpdate> updates,
        CancellationToken cancellationToken
    )
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        configuration.UpdateOffset = updates[^1].UpdateId + 1;

        foreach (var update in updates)
        {
            if (
                update.Chat is null
                || !TryReadPairingToken(update.Text, out var token)
                || !IsValidPairingToken(configuration, token)
            )
            {
                continue;
            }

            configuration.ChatId = update.Chat.Id;
            configuration.ChatName = GetChatName(update.Chat);
            configuration.PairingTokenHash = null;
            configuration.PairingExpiresAt = null;
            configuration.ForwardNotificationsAfterId = await GetLatestNotificationIdAsync(
                cancellationToken
            );

            break;
        }

        await configurationRepository.SaveChangesAsync(cancellationToken);
        configurationCache.Invalidate();
    }

    private async Task PrepareDeliveriesAsync(
        TelegramConfigurationState configuration,
        CancellationToken cancellationToken
    )
    {
        var notificationIds = await readRepository.GetForwardableNotificationIdsAsync(
            configuration.ForwardNotificationsAfterId,
            configuration.ForwardInfo,
            configuration.ForwardWarning,
            configuration.ForwardError,
            PrepareBatchSize,
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

        await AdvanceForwardWatermarkAsync(
            configuration.ForwardNotificationsAfterId,
            cancellationToken
        );
    }

    private async Task AdvanceForwardWatermarkAsync(
        int currentAfterId,
        CancellationToken cancellationToken
    )
    {
        var firstUnhandled = await readRepository.GetFirstUnhandledNotificationIdAsync(
            currentAfterId,
            cancellationToken
        );

        var newAfterId = firstUnhandled is null
            ? await readRepository.GetLatestNotificationIdAsync(cancellationToken)
            : firstUnhandled.Value - 1;

        if (newAfterId <= currentAfterId)
        {
            return;
        }

        var configuration = await GetConfigurationAsync(cancellationToken);
        configuration.ForwardNotificationsAfterId = newAfterId;
        await configurationRepository.SaveChangesAsync(cancellationToken);
        configurationCache.Invalidate();
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
                delivery.AttemptCount++;
                delivery.LastError = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                delivery.NextAttemptAt = timeProvider
                    .GetLocalNow()
                    .AddSeconds(Math.Min(300, 5 * Math.Pow(2, delivery.AttemptCount - 1)));
            }

            await deliveryRepository.SaveChangesAsync(cancellationToken);
        }
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

    private async Task EnsureCacheInitializedAsync(CancellationToken cancellationToken)
    {
        if (configurationCache.IsInitialized)
        {
            return;
        }

        var state = await readRepository.GetConfigurationStateAsync(cancellationToken);
        configurationCache.Set(state);
    }

    private async Task<TelegramConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken
    )
    {
        return await configurationRepository.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("Telegram is not configured.");
    }

    private async Task<int> GetLatestNotificationIdAsync(CancellationToken cancellationToken)
    {
        return await readRepository.GetLatestNotificationIdAsync(cancellationToken);
    }

    private static string NormalizeBaseUrl(string notificationBaseUrl)
    {
        if (
            !Uri.TryCreate(notificationBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        )
        {
            throw new InvalidOperationException(
                "The Bearcat URL must be an absolute HTTP or HTTPS URL."
            );
        }

        return notificationBaseUrl.TrimEnd('/');
    }

    private static bool TryReadPairingToken(string? text, out string token)
    {
        const string command = "/start ";

        if (text?.StartsWith(command, StringComparison.Ordinal) == true)
        {
            token = text[command.Length..].Trim();
            return token.Length > 0;
        }

        token = string.Empty;
        return false;
    }

    private bool IsValidPairingToken(TelegramConfiguration configuration, string token)
    {
        return configuration.PairingTokenHash is not null
            && configuration.PairingExpiresAt > timeProvider.GetLocalNow()
            && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(configuration.PairingTokenHash),
                SHA256.HashData(Encoding.UTF8.GetBytes(token))
            );
    }

    private static string GetChatName(TelegramChat chat)
    {
        var name = string.Join(
            ' ',
            new[] { chat.FirstName, chat.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))
        );
        return string.IsNullOrWhiteSpace(name) ? chat.Username ?? chat.Id.ToString() : name;
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
            $"{icon} Bearcat: {notification.NotificationSeverity}\n\n{notification.Message}"
        );

        if (relatedEntity is not null)
        {
            builder.Append(
                $"\n\n{DescribeEntityType(relatedEntity.EntityType)}: {relatedEntity.DisplayName}"
            );
        }

        builder.Append($"\n\n{url}");
        return builder.ToString();
    }

    private static string DescribeEntityType(string entityType)
    {
        return entityType switch
        {
            "Upload" => "Upload",
            "Archive" => "Archive",
            "LinkCrypterContainer" => "Link container",
            "Release" => "Release",
            _ => entityType,
        };
    }
}
