using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed class TelegramSettingsService(
    TelegramConfigurationService configurationService,
    ITelegramConfigurationRepository configurationRepository,
    ITelegramNotificationReadRepository readRepository,
    ITelegramDeliveryRepository deliveryRepository,
    ISecretProtector secretProtector,
    ITelegramClient telegramClient,
    TimeProvider timeProvider,
    UnreadableSecretsNotificationService unreadableSecretsNotificationService
)
{
    public async Task<TelegramSettings> GetSettingsAsync(
        string defaultBaseUrl,
        CancellationToken cancellationToken = default
    )
    {
        var configuration = await configurationService.GetCachedStateAsync(cancellationToken);

        if (configuration is null)
        {
            return new TelegramSettings(
                IsConfigured: false,
                HasUnreadableSecrets: false,
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
            HasUnreadableSecrets: configuration.HasUnreadableSecrets,
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

        await configurationService.SaveChangesAndInvalidateCacheAsync(cancellationToken);
        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );
    }

    public async Task SaveForwardedNotificationSeveritiesAsync(
        bool forwardInfo,
        bool forwardWarning,
        bool forwardError,
        CancellationToken cancellationToken = default
    )
    {
        var configuration = await configurationService.GetConfigurationAsync(cancellationToken);

        configuration.ForwardInfo = forwardInfo;
        configuration.ForwardWarning = forwardWarning;
        configuration.ForwardError = forwardError;

        await configurationService.SaveChangesAndInvalidateCacheAsync(cancellationToken);
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
            ForwardNotificationsAfterId = await readRepository.GetLatestNotificationIdAsync(
                cancellationToken
            ),
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
        configuration.HasUnreadableSecrets = false;
        configuration.BotUsername = bot.Username;
        configuration.ChatId = null;
        configuration.ChatName = null;
        configuration.PairingTokenHash = null;
        configuration.PairingExpiresAt = null;
        configuration.UpdateOffset = 0;
        configuration.ForwardNotificationsAfterId =
            await readRepository.GetLatestNotificationIdAsync(cancellationToken);
        await deliveryRepository.DeletePendingAsync(cancellationToken);
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
}
