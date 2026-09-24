using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class UploadLinksDialog(IScopedOperationRunner operationRunner)
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
            var result = await operationRunner.RunAsync(
                (IReleaseReadRepository repository) =>
                    repository.SearchUploadLinksAsync(
                        new ReleaseUploadLinkSearchQuery(
                            ReleaseId,
                            UploadId,
                            SelectedOnlineStateValue,
                            pageIndex,
                            pageSize
                        )
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
        pageIndex = page - 1;
        await RefreshLinksAsync();
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
        allUploadLinks = await operationRunner.RunAsync(
            (IReleaseReadRepository repository) =>
                repository.GetUploadLinksAsync(ReleaseId, UploadId, SelectedOnlineStateValue)
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
