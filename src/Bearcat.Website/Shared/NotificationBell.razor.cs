using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Shared;

public partial class NotificationBell(
    IServiceScopeFactory serviceScopeFactory,
    NavigationManager navigationManager
) : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource pollingCancellation = new();
    private IReadOnlyList<NotificationReadModel> latestNotifications = [];
    private int unresolvedCount;
    private bool isOpen;
    private bool isLoadingPreview;
    private bool openedByTriggerClick;
    private Task? pollingTask;

    private string BadgeText => unresolvedCount > 99 ? "99+" : unresolvedCount.ToString();

    protected override async Task OnInitializedAsync()
    {
        navigationManager.LocationChanged += HandleLocationChanged;
        await RefreshCountAsync();
        pollingTask = PollCountAsync(pollingCancellation.Token);
    }

    private void HandleTriggerClick()
    {
        if (openedByTriggerClick)
        {
            openedByTriggerClick = false;
            return;
        }

        if (isOpen)
        {
            isOpen = false;
        }
    }

    private async Task HandleOpenChangedAsync(bool open)
    {
        isOpen = open;

        if (open)
        {
            openedByTriggerClick = true;
            await LoadPreviewAsync();
        }
        else
        {
            openedByTriggerClick = false;
        }
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task LoadPreviewAsync()
    {
        isLoadingPreview = true;

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var readRepository =
                scope.ServiceProvider.GetRequiredService<INotificationReadRepository>();
            latestNotifications = await readRepository.GetLatestUnresolvedAsync(5);
            unresolvedCount = await readRepository.CountUnresolvedAsync();
        }
        finally
        {
            isLoadingPreview = false;
        }
    }

    private async Task ResolveFromPreviewAsync(int notificationId)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.ResolveAsync(notificationId);
        await LoadPreviewAsync();
    }

    private async Task RefreshCountAsync()
    {
        using var scope = serviceScopeFactory.CreateScope();
        var readRepository =
            scope.ServiceProvider.GetRequiredService<INotificationReadRepository>();
        unresolvedCount = await readRepository.CountUnresolvedAsync();
    }

    private async Task PollCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    await RefreshCountAsync();
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) { }
    }

    private static BadgeVariant GetNotificationVariant(NotificationType notificationType) =>
        notificationType switch
        {
            NotificationType.Error => BadgeVariant.Destructive,
            NotificationType.Warning => BadgeVariant.Secondary,
            _ => BadgeVariant.Outline,
        };

    private static string GetEntityIcon(string entityType) =>
        entityType switch
        {
            "Archive" => "archive",
            "LinkCrypterContainer" => "shield",
            "Upload" => "upload",
            _ => "link",
        };

    public async ValueTask DisposeAsync()
    {
        navigationManager.LocationChanged -= HandleLocationChanged;
        await pollingCancellation.CancelAsync();
        pollingCancellation.Dispose();

        if (pollingTask is not null)
        {
            try
            {
                await pollingTask;
            }
            catch (OperationCanceledException) { }
        }
    }
}
