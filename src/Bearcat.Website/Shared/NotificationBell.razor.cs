using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Shared;

public partial class NotificationBell(IServiceScopeFactory serviceScopeFactory)
    : ComponentBase,
        IAsyncDisposable
{
    private readonly CancellationTokenSource pollingCancellation = new();
    private IReadOnlyList<NotificationDto> latestNotifications = [];
    private int unresolvedCount;
    private bool isOpen;
    private bool isLoadingPreview;
    private Task? pollingTask;

    private string BadgeText => unresolvedCount > 99 ? "99+" : unresolvedCount.ToString();

    protected override async Task OnInitializedAsync()
    {
        await RefreshCountAsync();
        pollingTask = PollCountAsync(pollingCancellation.Token);
    }

    private async Task HandleOpenChangedAsync(bool open)
    {
        isOpen = open;

        if (open)
        {
            await LoadPreviewAsync();
        }
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
