using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageNotifications;

public partial class NotificationDetailPage(
    NavigationManager navigationManager,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public int NotificationId { get; set; }

    private NotificationReadModel notification = null!;
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        await LoadNotificationAsync();
    }

    private async Task LoadNotificationAsync()
    {
        var notificationReadModel = await operationRunner.RunAsync(
            (INotificationReadRepository repository) => repository.GetByIdAsync(NotificationId)
        );

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
        await operationRunner.RunAsync(
            (INotificationService service) => service.ResolveAsync(NotificationId)
        );
        await LoadNotificationAsync();
    }

    private void BackToNotifications()
    {
        navigationManager.NavigateTo("/notifications");
    }

    private static BadgeVariant GetNotificationVariant(NotificationSeverity notificationSeverity) =>
        notificationSeverity switch
        {
            NotificationSeverity.Error => BadgeVariant.Destructive,
            NotificationSeverity.Warning => BadgeVariant.Secondary,
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
