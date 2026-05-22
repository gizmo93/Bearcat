using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageNotifications;

public partial class NotificationDetailPage(NavigationManager navigationManager)
    : OwningComponentBase
{
    [Parameter]
    public int NotificationId { get; set; }

    private NotificationReadModel notification = null!;
    private INotificationReadRepository readRepository = null!;
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<INotificationReadRepository>();
        await LoadNotificationAsync();
    }

    private async Task LoadNotificationAsync()
    {
        var notificationReadModel = await readRepository.GetByIdAsync(NotificationId);

        if (notificationReadModel is null)
        {
            navigationManager.NotFound();
            return;
        }

        notification = notificationReadModel;
        isInitialized = true;
    }

    private async Task ResolveNotificationAsync()
    {
        var notificationService = ScopedServices.GetRequiredService<INotificationService>();
        await notificationService.ResolveAsync(NotificationId);
        await LoadNotificationAsync();
    }

    private void BackToNotifications()
    {
        navigationManager.NavigateTo("/notifications");
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
}
