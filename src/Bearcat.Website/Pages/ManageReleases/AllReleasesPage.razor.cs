using System.Globalization;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleases.Results;
using Bearcat.Website.Pages.ManageReleaseTemplates;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class AllReleasesPage(
    DialogService dialogService,
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    IJSRuntime js,
    PersistentComponentState applicationState,
    IHttpContextAccessor httpContextAccessor
) : IReleaseSearchUrlValues, IAsyncDisposable
{
    private const string ResultViewKey = "bearcat.releases.resultView";

    private IReadOnlyList<ReleaseSearchResultReadModel>? releases;
    private IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations = [];
    private IReadOnlyList<ArchiverDto> archiverOptions = [];
    private IReadOnlyList<LinkCrypterRegistrationReadModel> linkCrypterRegistrations = [];
    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private readonly ReleaseSelection selection = new();
    private readonly ReleaseKeyboardFocus keyboardFocus = new();
    private ReleaseSearchResultView resultView = ReleaseSearchResultView.List;
    private PersistingComponentStateSubscription persistSubscription;
    private DotNetObjectReference<AllReleasesPage>? dotNetReference;
    private IJSObjectReference? keyboardHandle;
    private bool isQuickLookOpen;
    private bool scrollFocusedReleaseIntoView;
    private ReleaseSearchQuery searchQuery = new();
    private ReleaseOnlineStateCounts? onlineStateCounts;
    private SearchUrlState<ReleaseSearchQuery>? loadedState;
    private int totalCount;
    private int pageIndex;
    private int pageSize = SearchUrlParameters.DefaultPageSize;
    private bool isLoading;

    [SupplyParameterFromQuery(Name = "q")]
    public string? SearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "type")]
    public string? ReleaseType { get; set; }

    [SupplyParameterFromQuery(Name = "content")]
    public string? ReleaseContentType { get; set; }

    [SupplyParameterFromQuery(Name = "lang")]
    public string? Language { get; set; }

    [SupplyParameterFromQuery(Name = "state")]
    public string? OnlineState { get; set; }

    [SupplyParameterFromQuery(Name = "hoster")]
    public int? HosterRegistrationId { get; set; }

    [SupplyParameterFromQuery(Name = "archiver")]
    public string? ArchiverName { get; set; }

    [SupplyParameterFromQuery(Name = "crypter")]
    public int? LinkCrypterRegistrationId { get; set; }

    [SupplyParameterFromQuery(Name = "group")]
    public int? ReleaseGroupId { get; set; }

    [SupplyParameterFromQuery(Name = "posted")]
    public string? PostedLocationUrl { get; set; }

    [SupplyParameterFromQuery(Name = "link")]
    public string? DownloadLink { get; set; }

    [SupplyParameterFromQuery(Name = "file")]
    public string? ArchiveFileName { get; set; }

    [SupplyParameterFromQuery(Name = "upload")]
    public string? UploadId { get; set; }

    [SupplyParameterFromQuery(Name = "sort")]
    public string? SortOrder { get; set; }

    [SupplyParameterFromQuery(Name = "page")]
    public int? Page { get; set; }

    [SupplyParameterFromQuery(Name = "size")]
    public int? PageSize { get; set; }

    private string ReleaseTotalCountText =>
        L[
            totalCount == 1 ? "ReleaseTotalCountOne" : "ReleaseTotalCount",
            totalCount.ToString("N0", CultureInfo.CurrentCulture)
        ];

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

    private IReadOnlyList<int> VisibleReleaseIds =>
        releases?.Select(release => release.ReleaseId).ToList() ?? [];

    private bool HasActiveFilters => searchQuery != EmptySearchQuery;

    private ReleaseSearchQuery EmptySearchQuery => new(SortOrder: searchQuery.SortOrder);

    private ReleaseSearchResultReadModel? FocusedRelease =>
        releases?.FirstOrDefault(release => release.ReleaseId == keyboardFocus.FocusedReleaseId);

    private bool IsQuickLookOpen => isQuickLookOpen && FocusedRelease is not null;

    private string SectionClass =>
        selection.Count > 0
            ? "bearcat-releases-page space-y-4 pb-20"
            : "bearcat-releases-page space-y-4";

    protected override async Task OnInitializedAsync()
    {
        resultView = RestoreResultView();
        persistSubscription = applicationState.RegisterOnPersisting(PersistResultView);

        hosterRegistrations = await operationRunner.RunAsync(
            (IHosterConfigurationReadRepository repository) => repository.GetAllRegistrationsAsync()
        );
        archiverOptions = operationRunner.Run(
            (IReleaseReadRepository repository) => repository.GetArchiverFilterOptions()
        );
        linkCrypterRegistrations = await operationRunner.RunAsync(
            (ILinkCrypterRegistrationReadRepository repository) => repository.GetAllAsync()
        );
        releaseGroups = await operationRunner.RunAsync(
            (IReleaseGroupReadRepository repository) => repository.GetAllAsync()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        var state = ReleaseSearchUrl.Parse(this);

        if (state == loadedState)
        {
            return;
        }

        if (loadedState is not null && loadedState.Query != state.Query)
        {
            selection.Clear();
        }

        keyboardFocus.Clear();
        isQuickLookOpen = false;

        searchQuery = state.Query;
        pageIndex = state.PageIndex;
        pageSize = state.PageSize;
        await RefreshReleasesAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (firstRender)
            {
                dotNetReference = DotNetObjectReference.Create(this);
                keyboardHandle = await js.InvokeAsync<IJSObjectReference>(
                    "bearcat.releaseSearchKeyboard.attach",
                    dotNetReference
                );
            }

            if (scrollFocusedReleaseIntoView && keyboardFocus.FocusedReleaseId is { } releaseId)
            {
                scrollFocusedReleaseIntoView = false;
                await js.InvokeVoidAsync(
                    "bearcat.releaseSearchKeyboard.scrollReleaseIntoView",
                    releaseId
                );
            }
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public void HandleKeyboardShortcut(string key, bool shiftKey)
    {
        switch (key)
        {
            case "j" or "ArrowDown":
                keyboardFocus.MoveToNext(VisibleReleaseIds);
                scrollFocusedReleaseIntoView = true;
                break;
            case "k" or "ArrowUp":
                keyboardFocus.MoveToPrevious(VisibleReleaseIds);
                scrollFocusedReleaseIntoView = true;
                break;
            case "x":
                ToggleFocusedReleaseSelection(extendRange: shiftKey);
                break;
            case "Space":
                ToggleQuickLook();
                break;
            case "Enter":
                OpenFocusedRelease();
                break;
            case "Escape":
                selection.Clear();
                break;
        }

        StateHasChanged();
    }

    private void ToggleFocusedReleaseSelection(bool extendRange)
    {
        if (keyboardFocus.FocusedReleaseId is { } releaseId)
        {
            selection.Toggle(VisibleReleaseIds, releaseId, extendRange);
        }
    }

    private void ToggleQuickLook()
    {
        if (IsQuickLookOpen)
        {
            isQuickLookOpen = false;
            return;
        }

        if (keyboardFocus.FocusedReleaseId is null)
        {
            keyboardFocus.MoveToNext(VisibleReleaseIds);
            scrollFocusedReleaseIntoView = true;
        }

        isQuickLookOpen = true;
    }

    private void OpenFocusedRelease()
    {
        if (FocusedRelease is { } release)
        {
            navigationManager.NavigateTo($"/releases/{release.ReleaseId}");
        }
    }

    private void OpenQuickLook(ReleaseSearchResultReadModel release)
    {
        keyboardFocus.Focus(release.ReleaseId);
        isQuickLookOpen = true;
    }

    private void SetQuickLookOpen(bool open)
    {
        isQuickLookOpen = open;
    }

    private ReleaseSearchResultView RestoreResultView()
    {
        if (
            applicationState.TryTakeFromJson<ReleaseSearchResultView>(
                ResultViewKey,
                out var persisted
            )
        )
        {
            return persisted;
        }

        return
            httpContextAccessor.HttpContext?.Request.Cookies.TryGetValue(
                ResultViewKey,
                out var cookie
            ) == true
            && Enum.TryParse<ReleaseSearchResultView>(cookie, out var view)
            && Enum.IsDefined(view)
            ? view
            : ReleaseSearchResultView.List;
    }

    private Task PersistResultView()
    {
        applicationState.PersistAsJson(ResultViewKey, resultView);
        return Task.CompletedTask;
    }

    private async Task ChangeResultViewAsync(ReleaseSearchResultView view)
    {
        resultView = view;

        try
        {
            await js.InvokeVoidAsync("bearcat.setCookie", ResultViewKey, view.ToString());
        }
        catch (JSException) { }
    }

    private string GetPageUri(int page)
    {
        return ReleaseSearchUrl.Build(searchQuery, page, pageSize);
    }

    private async Task DeleteReleaseAsync(ReleaseSearchResultReadModel release)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteReleaseTitle", release.Name],
            L["DeleteReleaseConfirmation", release.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await operationRunner.RunAsync(
            (ReleaseService service) => service.DeleteAsync(release.ReleaseId)
        );
        await RefreshReleasesAsync();
    }

    private async Task ShowAddReleaseDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseDialog>(
            new DialogOpenOptions
            {
                Title = L["CreateRelease"],
                Description = L["CreateReleaseDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await RefreshReleasesAsync();
        }
    }

    private async Task ShowAddReleaseFromTemplateDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<CreateReleaseFromTemplateDialog>(
            new DialogOpenOptions
            {
                Title = L["CreateReleaseFromTemplate"],
                Description = L["CreateReleaseFromTemplateDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await RefreshReleasesAsync();
        }
    }

    private async Task ShowEditReleaseDialogAsync(ReleaseSearchResultReadModel release)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseDialog.ReleaseId)] = release.ReleaseId,
            [nameof(CreateOrEditReleaseDialog.FormModel)] = new ReleaseFormModel
            {
                Name = release.Name,
                FolderPath = release.ReleaseFolderPath ?? string.Empty,
                ReleaseType = release.ReleaseType,
                ReleaseContentType = release.ReleaseContentType,
                PrimaryLanguageCode = release.PrimaryLanguageCode ?? string.Empty,
                ReleaseGroupId = release.ReleaseGroupId,
                IsEdit = true,
            },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", release.Name],
                Description = L["EditReleaseDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await RefreshReleasesAsync();
        }
    }

    private async Task RefreshReleasesAsync()
    {
        isLoading = true;

        try
        {
            var result = await operationRunner.RunAsync(
                (IReleaseReadRepository repository) =>
                    repository.SearchReleaseResultsAsync(
                        searchQuery with
                        {
                            PageIndex = pageIndex,
                            PageSize = pageSize,
                        }
                    )
            );

            releases = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;
            selection.KeepOnly(VisibleReleaseIds);
            keyboardFocus.KeepOnly(VisibleReleaseIds);

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshReleasesAsync();
                return;
            }

            onlineStateCounts = await operationRunner.RunAsync(
                (IReleaseReadRepository repository) =>
                    repository.CountReleasesByOnlineStateAsync(searchQuery)
            );
            loadedState = new SearchUrlState<ReleaseSearchQuery>(searchQuery, pageIndex, pageSize);
        }
        finally
        {
            isLoading = false;
        }
    }

    private Task ApplySearchAsync(ReleaseSearchQuery query)
    {
        return NavigateToSearchAsync(query, replaceHistoryEntry: false);
    }

    private Task ApplyTypedSearchAsync(ReleaseSearchQuery query)
    {
        return NavigateToSearchAsync(query, replaceHistoryEntry: true);
    }

    private Task ApplyOnlineStateAsync(OnlineState? onlineState)
    {
        return ApplySearchAsync(searchQuery with { OnlineState = onlineState });
    }

    private Task ApplySortOrderAsync(ReleaseSearchSortOrder sortOrder)
    {
        return ApplySearchAsync(searchQuery with { SortOrder = sortOrder });
    }

    private Task ResetFiltersAsync()
    {
        return ApplySearchAsync(EmptySearchQuery);
    }

    private async Task NavigateToSearchAsync(ReleaseSearchQuery query, bool replaceHistoryEntry)
    {
        var targetUri = navigationManager
            .ToAbsoluteUri(ReleaseSearchUrl.Build(query, page: 1, pageSize))
            .ToString();

        if (targetUri == navigationManager.Uri)
        {
            await RefreshReleasesAsync();
            return;
        }

        navigationManager.NavigateTo(targetUri, replace: replaceHistoryEntry);
    }

    private void ChangePageSize(int selectedPageSize)
    {
        navigationManager.NavigateTo(
            ReleaseSearchUrl.Build(searchQuery, page: 1, selectedPageSize)
        );
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        persistSubscription.Dispose();

        if (keyboardHandle is not null)
        {
            try
            {
                await keyboardHandle.InvokeVoidAsync("detach");
                await keyboardHandle.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }

        dotNetReference?.Dispose();
    }
}
