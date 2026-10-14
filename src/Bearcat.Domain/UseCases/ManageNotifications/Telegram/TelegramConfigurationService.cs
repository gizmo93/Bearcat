using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;

namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed class TelegramConfigurationService(
    ITelegramConfigurationRepository configurationRepository,
    ITelegramNotificationReadRepository readRepository,
    TelegramConfigurationCache configurationCache
)
{
    public async Task<TelegramConfigurationState?> GetCachedStateAsync(
        CancellationToken cancellationToken
    )
    {
        await EnsureCacheInitializedAsync(cancellationToken);

        return configurationCache.Current;
    }

    public async Task<TelegramConfigurationState?> GetCachedStateWithReadableBotTokenAsync(
        CancellationToken cancellationToken
    )
    {
        await EnsureCacheInitializedAsync(cancellationToken);

        return configurationCache.Current is { HasUnreadableSecrets: false } configuration
            ? configuration
            : null;
    }

    public async Task<TelegramConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken
    )
    {
        return await configurationRepository.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("Telegram is not configured.");
    }

    public async Task SaveChangesAndInvalidateCacheAsync(CancellationToken cancellationToken)
    {
        await configurationRepository.SaveChangesAsync(cancellationToken);
        configurationCache.Invalidate();
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
}
