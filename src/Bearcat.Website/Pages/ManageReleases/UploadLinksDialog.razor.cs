using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class UploadLinksDialog(IServiceScopeFactory serviceScopeFactory)
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int UploadId { get; set; }

    [Parameter]
    public string UploadConfigName { get; set; } = null!;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private readonly int[] pageSizes = [5, 10, 20, 50, 100];
    private IReadOnlyList<ReleaseUploadLinkReadModel> links = [];
    private IReadOnlyList<string> allUploadLinks = [];
    private int totalCount;
    private int pageIndex;
    private int pageSize = 5;
    private bool isInitialized;
    private bool isLoading;
    private bool showFileColumn;
    private int selectedOnlineState;

    private int CurrentPage => totalCount == 0 ? 1 : pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * pageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * pageSize);
    private string CopyTextAreaId => $"upload-links-copy-{UploadId}";
    private string LinksText => string.Join(Environment.NewLine, allUploadLinks);
    private string LinksTableKey =>
        $"{showFileColumn}-{selectedOnlineState}-{pageIndex}-{pageSize}";

    private IEnumerable<SelectOption<int>> PageSizeOptions =>
        pageSizes.Select(size => new SelectOption<int>(size, size.ToString()));

    private OnlineState? SelectedOnlineStateValue =>
        selectedOnlineState == 0 ? null : (OnlineState)selectedOnlineState;

    private IEnumerable<SelectOption<int>> OnlineStateOptions =>
        new SelectOption<int>[]
        {
            new(0, L["AnyOnlineState"]),
            new((int)OnlineState.Online, L.Localize(OnlineState.Online)),
            new((int)OnlineState.PartiallyOnline, L.Localize(OnlineState.PartiallyOnline)),
            new((int)OnlineState.Offline, L.Localize(OnlineState.Offline)),
            new((int)OnlineState.Unknown, L.Localize(OnlineState.Unknown)),
        };

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
        await RefreshAllUploadLinksAsync();
        await RefreshLinksAsync();
        isInitialized = true;
    }

    private async Task RefreshLinksAsync()
    {
        isLoading = true;

        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var readRepository = scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();
            var result = await readRepository.SearchUploadLinksAsync(
                new ReleaseUploadLinkSearchQuery(
                    ReleaseId,
                    UploadId,
                    SelectedOnlineStateValue,
                    pageIndex,
                    pageSize
                )
            );

            links = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshLinksAsync();
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task GoToPageAsync(int page)
    {
        var nextPageIndex = Math.Clamp(page - 1, 0, TotalPages - 1);
        if (nextPageIndex == pageIndex)
        {
            return;
        }

        pageIndex = nextPageIndex;
        await RefreshLinksAsync();
    }

    private async Task GoToPreviousPageAsync()
    {
        await GoToPageAsync(CurrentPage - 1);
    }

    private async Task GoToNextPageAsync()
    {
        await GoToPageAsync(CurrentPage + 1);
    }

    private async Task OnPageSizeChangedAsync()
    {
        pageIndex = 0;
        await RefreshLinksAsync();
    }

    private async Task OnOnlineStateFilterChangedAsync()
    {
        pageIndex = 0;
        await RefreshAllUploadLinksAsync();
        await RefreshLinksAsync();
    }

    private async Task RefreshAllUploadLinksAsync()
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var readRepository = scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();
        allUploadLinks = await readRepository.GetUploadLinksAsync(
            ReleaseId,
            UploadId,
            SelectedOnlineStateValue
        );
    }

    private void ToggleFileColumn()
    {
        showFileColumn = !showFileColumn;
    }

    private static string GetFileName(string filePath)
    {
        return Path.GetFileName(filePath);
    }

    private async Task CloseAsync()
    {
        await DialogRef.CancelAsync();
    }
}
