using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Dto;
using Bearcat.Domain.UseCases.ManageNotifications.ReadModels;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageNotifications;

public partial class NotificationsPage(
    NavigationManager navigationManager,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    private IReadOnlyList<NotificationReadModel> notifications = [];
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

    private IReadOnlyList<int?> PaginationItems
    {
        get
        {
            if (TotalPages <= 7)
            {
                return Enumerable.Range(1, TotalPages).Select(page => (int?)page).ToList();
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
        await RefreshNotificationsAsync();
    }

    private async Task RefreshNotificationsAsync()
    {
        isLoading = true;

        try
        {
            var (result, currentUnresolvedCount) = await operationRunner.RunAsync(
                async (INotificationReadRepository repository) =>
                {
                    var searchResult = await repository.SearchAsync(
                        new NotificationSearchQuery(pageIndex, pageSize, includeResolved)
                    );
                    var count = await repository.CountUnresolvedAsync();
                    return (searchResult, count);
                }
            );

            notifications = result.Items;
            totalCount = result.TotalCount;
            unresolvedCount = currentUnresolvedCount;
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
        await operationRunner.RunAsync(
            (INotificationService service) => service.ResolveAsync(notificationId)
        );
        await RefreshNotificationsAsync();
    }

    private async Task ResolveAllNotificationsAsync()
    {
        await operationRunner.RunAsync((INotificationService service) => service.ResolveAllAsync());
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
