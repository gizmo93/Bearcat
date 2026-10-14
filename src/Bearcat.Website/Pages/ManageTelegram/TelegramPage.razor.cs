using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Website.Formatting;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageTelegram;

public partial class TelegramPage(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    ToastService toastService,
    DialogService dialogService,
    TimeProvider timeProvider
)
{
    private TelegramSettings settings = null!;
    private TelegramDeliveryStatus? deliveryStatus;
    private string? botToken;
    private string notificationBaseUrl = string.Empty;
    private string? pairingUrl;
    private bool forwardInfo;
    private bool forwardWarning;
    private bool forwardError;
    private bool isLoading = true;
    private bool isSavingConfiguration;
    private bool isSavingForwardedNotificationSeverities;
    private bool isSendingTest;
    private bool isStartingPairing;
    private bool isRefreshing;
    private bool isDisconnecting;

    private bool KeepsStoredBotToken =>
        settings is { IsConfigured: true, HasUnreadableSecrets: false };

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        settings = await operationRunner.RunAsync(
            (TelegramSettingsService service) => service.GetSettingsAsync(navigationManager.BaseUri)
        );
        notificationBaseUrl = settings.NotificationBaseUrl;
        forwardInfo = settings.ForwardInfo;
        forwardWarning = settings.ForwardWarning;
        forwardError = settings.ForwardError;
        deliveryStatus = settings.IsConnected
            ? await operationRunner.RunAsync(
                (TelegramDeliveryService service) => service.GetDeliveryStatusAsync()
            )
            : null;
        isLoading = false;
    }

    private async Task SaveConfigurationAsync()
    {
        if (settings.HasUnreadableSecrets && string.IsNullOrWhiteSpace(botToken))
        {
            toastService.Error(L["TelegramBotTokenRequired"]);
            return;
        }

        isSavingConfiguration = true;
        try
        {
            await operationRunner.RunAsync(
                (TelegramSettingsService service) =>
                    service.SaveConfigurationAsync(botToken, notificationBaseUrl)
            );
            botToken = null;
            pairingUrl = null;
            await LoadAsync();
            toastService.Success(L["TelegramConfigurationSaved"]);
        }
        catch (Exception exception)
        {
            toastService.Error(exception.Message);
        }
        finally
        {
            isSavingConfiguration = false;
        }
    }

    private async Task SaveForwardedNotificationSeveritiesAsync()
    {
        isSavingForwardedNotificationSeverities = true;
        try
        {
            await operationRunner.RunAsync(
                (TelegramSettingsService service) =>
                    service.SaveForwardedNotificationSeveritiesAsync(
                        forwardInfo,
                        forwardWarning,
                        forwardError
                    )
            );
            toastService.Success(L["TelegramLevelsSaved"]);
        }
        catch (Exception exception)
        {
            toastService.Error(exception.Message);
        }
        finally
        {
            isSavingForwardedNotificationSeverities = false;
        }
    }

    private async Task CreatePairingLinkAsync()
    {
        isStartingPairing = true;
        try
        {
            pairingUrl = await operationRunner.RunAsync(
                (TelegramPairingService service) => service.CreatePairingLinkAsync()
            );
        }
        catch (Exception exception)
        {
            toastService.Error(exception.Message);
        }
        finally
        {
            isStartingPairing = false;
        }
    }

    private async Task RefreshConnectionAsync()
    {
        isRefreshing = true;
        try
        {
            await LoadAsync();
            if (settings.IsConnected)
            {
                pairingUrl = null;
                toastService.Success(L["TelegramConnected"]);
            }
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private async Task SendTestMessageAsync()
    {
        isSendingTest = true;
        try
        {
            await operationRunner.RunAsync(
                (TelegramDeliveryService service) => service.SendTestMessageAsync()
            );
            toastService.Success(L["TestNotificationSent"]);
        }
        catch (Exception exception)
        {
            toastService.Error(exception.Message);
        }
        finally
        {
            isSendingTest = false;
        }
    }

    private async Task DisconnectAsync()
    {
        var result = await dialogService.ConfirmAsync(
            L["DisconnectTelegram"],
            L["DisconnectTelegramConfirmation"],
            new ConfirmDialogOptions
            {
                ConfirmText = L["DisconnectTelegram"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        isDisconnecting = true;
        try
        {
            await operationRunner.RunAsync(
                (TelegramPairingService service) => service.DisconnectChatAsync()
            );
            pairingUrl = null;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            toastService.Error(exception.Message);
        }
        finally
        {
            isDisconnecting = false;
        }
    }

    private string HumanizeLastDelivered(DateTime lastDeliveredAt)
    {
        return timeProvider.Humanize(lastDeliveredAt);
    }
}
