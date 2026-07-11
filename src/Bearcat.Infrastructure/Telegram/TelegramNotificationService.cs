using System.Security.Cryptography;
using System.Text;
using Bearcat.Abstractions.Notifications;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Infrastructure.Telegram;

public sealed class TelegramNotificationService(
    IBearcatReadDbContext readDbContext,
    IBearcatWriteDbContext writeDbContext,
    ISecretProtector secretProtector,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    TelegramConfigurationCache configurationCache
) : ITelegramNotificationProcessor
{
    private static readonly TimeSpan PairingDuration = TimeSpan.FromMinutes(10);
    private const int DeliveryBatchSize = 100;

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
        await EnsureCacheInitializedAsync(cancellationToken);

        var baseUrl = NormalizeBaseUrl(notificationBaseUrl);

        var configuration = await writeDbContext.TelegramConfigurations.SingleOrDefaultAsync(
            cancellationToken
        );

        if (configuration is null)
        {
            configuration = await CreateConfigurationAsync(botToken, baseUrl, cancellationToken);
            writeDbContext.TelegramConfigurations.Add(configuration);
        }
        else
        {
            await UpdateConfigurationAsync(configuration, botToken, baseUrl, cancellationToken);
        }

        await writeDbContext.SaveChangesAsync(cancellationToken);
        configurationCache.Update(configuration);
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

        await writeDbContext.SaveChangesAsync(cancellationToken);
        configurationCache.Update(configuration);
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
        await writeDbContext.SaveChangesAsync(cancellationToken);

        configurationCache.Update(configuration);

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

        await DeletePendingDeliveriesAsync(cancellationToken);
        await writeDbContext.SaveChangesAsync(cancellationToken);

        configurationCache.Update(configuration);
    }

    public async Task SendTestMessageAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCacheInitializedAsync(cancellationToken);

        var configuration = configurationCache.Current;

        if (configuration?.ChatId is null)
        {
            throw new InvalidOperationException("Telegram is not connected.");
        }

        await CreateClient(botToken: secretProtector.Unprotect(configuration.EncryptedBotToken))
            .SendMessage(
                chatId: configuration.ChatId.Value,
                text: "Bearcat Telegram notifications are connected.",
                cancellationToken: cancellationToken
            );
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
        if (updates.Length > 0)
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

        var deliveries = await GetPendingDeliveriesAsync(cancellationToken);

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

        var bot = await CreateClient(botToken).GetMe(cancellationToken);
        return new TelegramConfiguration
        {
            EncryptedBotToken = secretProtector.Protect(botToken),
            BotUsername = bot.Username!,
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

        var bot = await CreateClient(botToken).GetMe(cancellationToken);

        configuration.EncryptedBotToken = secretProtector.Protect(botToken);
        configuration.BotUsername = bot.Username!;
        configuration.ChatId = null;
        configuration.ChatName = null;
        configuration.PairingTokenHash = null;
        configuration.PairingExpiresAt = null;
        configuration.UpdateOffset = 0;
        configuration.ForwardNotificationsAfterId = await GetLatestNotificationIdAsync(
            cancellationToken
        );
        await DeletePendingDeliveriesAsync(cancellationToken);
    }

    private async Task ClearExpiredPairingAsync(CancellationToken cancellationToken)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);

        configuration.PairingTokenHash = null;
        configuration.PairingExpiresAt = null;

        await writeDbContext.SaveChangesAsync(cancellationToken);
        configurationCache.Update(configuration);
    }

    private async Task<Update[]> ReceivePairingUpdatesAsync(
        TelegramConfigurationState pairing,
        CancellationToken cancellationToken
    )
    {
        var client = CreateClient(secretProtector.Unprotect(pairing.EncryptedBotToken));

        return await client.GetUpdates(
            offset: (int)pairing.UpdateOffset,
            timeout: 30,
            allowedUpdates: [UpdateType.Message],
            cancellationToken: cancellationToken
        );
    }

    private async Task ApplyPairingUpdatesAsync(
        Update[] updates,
        CancellationToken cancellationToken
    )
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        configuration.UpdateOffset = updates[^1].Id + 1;

        foreach (var update in updates)
        {
            if (
                !TryReadPairingToken(update, out var token)
                || !IsValidPairingToken(configuration, token)
            )
            {
                continue;
            }

            configuration.ChatId = update.Message!.Chat.Id;
            configuration.ChatName = GetChatName(update.Message.Chat);
            configuration.PairingTokenHash = null;
            configuration.PairingExpiresAt = null;
            configuration.ForwardNotificationsAfterId = await GetLatestNotificationIdAsync(
                cancellationToken
            );

            break;
        }

        await writeDbContext.SaveChangesAsync(cancellationToken);
        configurationCache.Update(configuration);
    }

    private async Task<List<TelegramDelivery>> GetPendingDeliveriesAsync(
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetLocalNow();

        return await writeDbContext
            .TelegramDeliveries.Include(delivery => delivery.Notification)
            .Where(delivery =>
                delivery.DeliveredAt == null
                && (delivery.NextAttemptAt == null || delivery.NextAttemptAt <= now)
            )
            .OrderBy(delivery => delivery.Id)
            .Take(10)
            .ToListAsync(cancellationToken);
    }

    private async Task SendDeliveriesAsync(
        TelegramConfigurationState configuration,
        List<TelegramDelivery> deliveries,
        CancellationToken cancellationToken
    )
    {
        var client = CreateClient(secretProtector.Unprotect(configuration.EncryptedBotToken));

        foreach (var delivery in deliveries)
        {
            try
            {
                await client.SendMessage(
                    chatId: configuration.ChatId!.Value,
                    text: CreateMessage(configuration, delivery.Notification),
                    cancellationToken: cancellationToken
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

            await writeDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task PrepareDeliveriesAsync(
        TelegramConfigurationState configuration,
        CancellationToken cancellationToken
    )
    {
        var notificationIds = await readDbContext
            .Notifications.Where(notification =>
                notification.Id > configuration.ForwardNotificationsAfterId
                && notification.ResolvedAt == null
                && (
                    (
                        notification.NotificationType == NotificationType.Info
                        && configuration.ForwardInfo
                    )
                    || (
                        notification.NotificationType == NotificationType.Warning
                        && configuration.ForwardWarning
                    )
                    || (
                        notification.NotificationType == NotificationType.Error
                        && configuration.ForwardError
                    )
                )
                && !readDbContext.TelegramDeliveries.Any(delivery =>
                    delivery.NotificationId == notification.Id
                )
            )
            .OrderBy(notification => notification.Id)
            .Select(notification => notification.Id)
            .Take(DeliveryBatchSize)
            .ToListAsync(cancellationToken);

        foreach (var notificationId in notificationIds)
        {
            writeDbContext.TelegramDeliveries.Add(
                new TelegramDelivery
                {
                    NotificationId = notificationId,
                    CreatedAt = timeProvider.GetLocalNow(),
                }
            );
        }

        if (notificationIds.Count > 0)
        {
            await writeDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private TelegramBotClient CreateClient(string botToken)
    {
        return new TelegramBotClient(botToken, httpClientFactory.CreateClient("telegram"));
    }

    private async Task<TelegramConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken
    )
    {
        return await writeDbContext.TelegramConfigurations.SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Telegram is not configured.");
    }

    private async Task EnsureCacheInitializedAsync(CancellationToken cancellationToken)
    {
        if (configurationCache.IsInitialized)
        {
            return;
        }

        var configuration = await readDbContext.TelegramConfigurations.SingleOrDefaultAsync(
            cancellationToken
        );

        configurationCache.Initialize(configuration);
    }

    private async Task<int> GetLatestNotificationIdAsync(CancellationToken cancellationToken)
    {
        return await readDbContext.Notifications.MaxAsync(
                notification => (int?)notification.Id,
                cancellationToken
            ) ?? 0;
    }

    private async Task DeletePendingDeliveriesAsync(CancellationToken cancellationToken)
    {
        await writeDbContext
            .TelegramDeliveries.Where(delivery => delivery.DeliveredAt == null)
            .ExecuteDeleteAsync(cancellationToken);
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

    private static bool TryReadPairingToken(Update update, out string token)
    {
        const string command = "/start ";

        var text = update.Message?.Text;
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

    private static string GetChatName(Chat chat)
    {
        var name = string.Join(
            ' ',
            new[] { chat.FirstName, chat.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))
        );
        return string.IsNullOrWhiteSpace(name) ? chat.Username ?? chat.Id.ToString() : name;
    }

    private static string CreateMessage(
        TelegramConfigurationState configuration,
        Notification notification
    )
    {
        var icon = notification.NotificationType switch
        {
            NotificationType.Info => "ℹ️",
            NotificationType.Warning => "⚠️",
            NotificationType.Error => "🔴",
            _ => "🔔",
        };
        var url = $"{configuration.NotificationBaseUrl}/notifications/{notification.Id}";
        return $"{icon} Bearcat: {notification.NotificationType}\n\n{notification.Message}\n\n{url}";
    }
}
