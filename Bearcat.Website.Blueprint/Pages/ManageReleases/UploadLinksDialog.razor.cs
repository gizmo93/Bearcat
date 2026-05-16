using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Blueprint.Localization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Blueprint.Pages.ManageReleases;

public partial class UploadLinksDialog(
    IReleaseReadRepository readRepository,
    IJSRuntime jsRuntime,
    ToastService toastService
) : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int UploadId { get; set; }

    [Parameter]
    public string UploadConfigName { get; set; } = null!;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private readonly int[] pageSizes = [10, 20, 50, 100];
    private IReadOnlyList<ReleaseUploadLinkDto> links = [];
    private IReadOnlyList<string> allUploadLinks = [];
    private int totalCount;
    private int pageIndex;
    private int pageSize = 10;
    private bool isInitialized;
    private bool isLoading;
    private bool isCopying;
    private bool isClipboardReady;
    private bool showFileColumn;
    private int selectedOnlineState;
    private IJSObjectReference? clipboardModule;

    private int CurrentPage => totalCount == 0 ? 1 : pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * pageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * pageSize);

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        clipboardModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "/_content/Bearcat.Website.Blueprint/bearcat.js"
        );
        isClipboardReady = true;
        StateHasChanged();
    }

    private async Task RefreshLinksAsync()
    {
        isLoading = true;

        try
        {
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

    private async Task CopyAllLinksAsync()
    {
        isCopying = true;

        try
        {
            await clipboardModule!.InvokeVoidAsync(
                "copyText",
                string.Join(Environment.NewLine, allUploadLinks)
            );

            toastService.Success(L["UploadLinksCopied", allUploadLinks.Count]);
        }
        catch
        {
            toastService.Error(L["CopyUploadLinksFailed"]);
        }
        finally
        {
            isCopying = false;
        }
    }

    private static string GetFileName(string filePath)
    {
        return Path.GetFileName(filePath);
    }

    private async Task CloseAsync()
    {
        await DialogRef.CancelAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (clipboardModule is not null)
        {
            await clipboardModule.DisposeAsync();
        }
    }
}
