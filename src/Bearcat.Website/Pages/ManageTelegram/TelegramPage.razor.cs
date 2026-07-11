using Bearcat.Infrastructure.Telegram;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageTelegram;

public partial class TelegramPage(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    ToastService toastService
)
{
    private TelegramSettings settings = null!;
    private string? botToken;
    private string notificationBaseUrl = string.Empty;
    private string? pairingUrl;
    private bool forwardInfo;
    private bool forwardWarning;
    private bool forwardError;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        settings = await operationRunner.RunAsync(
            (TelegramNotificationService service) =>
                service.GetSettingsAsync(navigationManager.BaseUri)
        );
        notificationBaseUrl = settings.NotificationBaseUrl;
        forwardInfo = settings.ForwardInfo;
        forwardWarning = settings.ForwardWarning;
        forwardError = settings.ForwardError;
        isLoading = false;
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            await operationRunner.RunAsync(
                (TelegramNotificationService service) =>
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
    }

    private async Task SaveLevelsAsync()
    {
        await operationRunner.RunAsync(
            (TelegramNotificationService service) =>
                service.SaveLevelsAsync(forwardInfo, forwardWarning, forwardError)
        );
        toastService.Success(L["TelegramLevelsSaved"]);
    }

    private async Task BeginPairingAsync()
    {
        pairingUrl = await operationRunner.RunAsync(
            (TelegramNotificationService service) => service.BeginPairingAsync()
        );
    }

    private async Task RefreshConnectionAsync()
    {
        await LoadAsync();
        if (settings.IsConnected)
        {
            pairingUrl = null;
            toastService.Success(L["TelegramConnected"]);
        }
    }

    private async Task SendTestMessageAsync()
    {
        try
        {
            await operationRunner.RunAsync(
                (TelegramNotificationService service) => service.SendTestMessageAsync()
            );
            toastService.Success(L["TestNotificationSent"]);
        }
        catch (Exception exception)
        {
            toastService.Error(exception.Message);
        }
    }

    private async Task DisconnectAsync()
    {
        await operationRunner.RunAsync(
            (TelegramNotificationService service) => service.DisconnectAsync()
        );
        pairingUrl = null;
        await LoadAsync();
    }
}
