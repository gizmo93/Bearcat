using Bearcat.Domain.Entities;

namespace Bearcat.Infrastructure.Telegram;

public sealed class TelegramConfigurationCache
{
    private readonly Lock sync = new();
    private volatile TelegramConfigurationState? configuration;
    private volatile bool initialized;

    internal bool IsInitialized => initialized;

    internal TelegramConfigurationState? Current =>
        initialized
            ? configuration
            : throw new InvalidOperationException(
                "The Telegram configuration cache is not initialized."
            );

    internal void Initialize(TelegramConfiguration? loadedConfiguration)
    {
        if (initialized)
        {
            return;
        }

        lock (sync)
        {
            if (initialized)
            {
                return;
            }

            configuration = loadedConfiguration is null
                ? null
                : TelegramConfigurationState.From(loadedConfiguration);

            initialized = true;
        }
    }

    internal void Update(TelegramConfiguration updatedConfiguration)
    {
        lock (sync)
        {
            configuration = TelegramConfigurationState.From(updatedConfiguration);
            initialized = true;
        }
    }
}

internal sealed record TelegramConfigurationState(
    string EncryptedBotToken,
    string BotUsername,
    string NotificationBaseUrl,
    long? ChatId,
    string? ChatName,
    bool ForwardInfo,
    bool ForwardWarning,
    bool ForwardError,
    string? PairingTokenHash,
    DateTime? PairingExpiresAt,
    long UpdateOffset,
    int ForwardNotificationsAfterId
)
{
    public static TelegramConfigurationState From(TelegramConfiguration configuration) =>
        new(
            configuration.EncryptedBotToken,
            configuration.BotUsername,
            configuration.NotificationBaseUrl,
            configuration.ChatId,
            configuration.ChatName,
            configuration.ForwardInfo,
            configuration.ForwardWarning,
            configuration.ForwardError,
            configuration.PairingTokenHash,
            configuration.PairingExpiresAt,
            configuration.UpdateOffset,
            configuration.ForwardNotificationsAfterId
        );
}
