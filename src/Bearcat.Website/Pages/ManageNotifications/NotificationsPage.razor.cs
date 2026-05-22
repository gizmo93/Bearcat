using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageNotifications;

public partial class NotificationsPage(NavigationManager navigationManager) : OwningComponentBase
{
    private IReadOnlyList<NotificationReadModel> notifications = [];
    private INotificationReadRepository readRepository = null!;
    private int totalCount;
    private int pageIndex;
    private int pageSize = 10;
    private int unresolvedCount;
    private bool includeResolved;
    private bool isLoading;

    private int CurrentPage => totalCount == 0 ? 1 : pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * pageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * pageSize);
    private string NotificationsTableKey =>
        $"{includeResolved}-{pageIndex}-{pageSize}-{totalCount}";

    private IEnumerable<int?> PaginationItems
    {
        get
        {
            if (TotalPages <= 7)
            {
                return Enumerable.Range(1, TotalPages).Select(page => (int?)page);
            }

            var pages = new List<int?> { 1 };
            var start = Math.Max(2, CurrentPage - 1);
            var end = Math.Min(TotalPages - 1, CurrentPage + 1);

            if (start > 2)
            {
                pages.Add(null);
            }

            pages.AddRange(Enumerable.Range(start, end - start + 1).Select(page => (int?)page));

            if (end < TotalPages - 1)
            {
                pages.Add(null);
            }

            pages.Add(TotalPages);
            return pages;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<INotificationReadRepository>();
        await RefreshNotificationsAsync();
    }

    private async Task RefreshNotificationsAsync()
    {
        isLoading = true;

        try
        {
            var result = await readRepository.SearchAsync(
                new NotificationSearchQuery(pageIndex, pageSize, includeResolved)
            );

            notifications = result.Items;
            totalCount = result.TotalCount;
            unresolvedCount = await readRepository.CountUnresolvedAsync();
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshNotificationsAsync();
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ResolveNotificationAsync(int notificationId)
    {
        var notificationService = ScopedServices.GetRequiredService<INotificationService>();
        await notificationService.ResolveAsync(notificationId);
        await RefreshNotificationsAsync();
    }

    private async Task ResolveAllNotificationsAsync()
    {
        var notificationService = ScopedServices.GetRequiredService<INotificationService>();
        await notificationService.ResolveAllAsync();
        pageIndex = 0;
        await RefreshNotificationsAsync();
    }

    private async Task OnIncludeResolvedChangedAsync()
    {
        pageIndex = 0;
        await RefreshNotificationsAsync();
    }

    private void OpenDetails(int notificationId)
    {
        navigationManager.NavigateTo($"/notifications/{notificationId}");
    }

    private async Task GoToPageAsync(int page)
    {
        var nextPageIndex = Math.Clamp(page - 1, 0, TotalPages - 1);
        if (nextPageIndex == pageIndex)
        {
            return;
        }

        pageIndex = nextPageIndex;
        await RefreshNotificationsAsync();
    }

    private async Task GoToPreviousPageAsync()
    {
        await GoToPageAsync(CurrentPage - 1);
    }

    private async Task GoToNextPageAsync()
    {
        await GoToPageAsync(CurrentPage + 1);
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
