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
using Bearcat.Website.Pages.ManageReleaseTemplates;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class AllReleasesPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager
) : IReleaseSearchUrlValues
{
    private const string NoBulkLanguageSelected = "__not_selected__";

    private IReadOnlyList<ReleaseReadModel> releases = [];
    private IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations = [];
    private IReadOnlyList<ArchiverDto> archiverOptions = [];
    private IReadOnlyList<LinkCrypterRegistrationReadModel> linkCrypterRegistrations = [];
    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private readonly HashSet<int> selectedReleaseIds = [];
    private ReleaseSearchQuery searchQuery = new();
    private ReleaseSearchUrlState? loadedState;
    private int totalCount;
    private int pageIndex;
    private int pageSize = ReleaseSearchUrl.DefaultPageSize;
    private int selectedBulkReleaseGroupId;
    private string selectedBulkPrimaryLanguageCode = NoBulkLanguageSelected;
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

    [SupplyParameterFromQuery(Name = "page")]
    public int? Page { get; set; }

    [SupplyParameterFromQuery(Name = "size")]
    public int? PageSize { get; set; }

    private int CurrentPage => totalCount == 0 ? 1 : pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * pageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * pageSize);
    private string ReleasesTableKey => $"{pageIndex}-{pageSize}-{searchQuery.GetHashCode()}";
    private bool AreAllVisibleReleasesSelected =>
        releases.Count > 0 && releases.All(r => selectedReleaseIds.Contains(r.ReleaseId));

    private static IReadOnlyList<SelectOption<int>> PageSizeOptions =>
        ReleaseSearchUrl
            .PageSizes.Select(size => new SelectOption<int>(size, size.ToString()))
            .ToList();

    private IReadOnlyList<SelectOption<int>> ReleaseGroupOptions =>
        [
            new(0, L["SelectReleaseGroup"]),
            .. releaseGroups.Select(group => new SelectOption<int>(
                group.ReleaseGroupId,
                group.Name
            )),
        ];

    private IReadOnlyList<SelectOption<string>> BulkLanguageOptions =>
        [
            new(NoBulkLanguageSelected, L["SelectLanguage"]),
            new(string.Empty, L["NotSet"]),
            .. CultureInfo
                .GetCultures(CultureTypes.NeutralCultures)
                .Where(culture => culture.TwoLetterISOLanguageName.Length == 2)
                .DistinctBy(culture => culture.TwoLetterISOLanguageName)
                .OrderBy(culture => culture.NativeName)
                .Select(culture => new SelectOption<string>(
                    culture.TwoLetterISOLanguageName,
                    culture.NativeName
                )),
        ];

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
            selectedReleaseIds.Clear();
        }

        searchQuery = state.Query;
        pageIndex = state.PageIndex;
        pageSize = state.PageSize;
        await RefreshReleasesAsync();
    }

    private string GetPageUri(int page)
    {
        return ReleaseSearchUrl.Build(searchQuery, page, pageSize);
    }

    private async Task DeleteReleaseAsync(ReleaseReadModel release)
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

    private async Task ShowEditReleaseDialogAsync(ReleaseReadModel release)
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
                    repository.SearchReleasesAsync(
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
            selectedReleaseIds.RemoveWhere(id => releases.All(r => r.ReleaseId != id));

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshReleasesAsync();
                return;
            }

            loadedState = new ReleaseSearchUrlState(searchQuery, pageIndex, pageSize);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ApplySearchAsync(ReleaseSearchQuery query)
    {
        var targetUri = navigationManager
            .ToAbsoluteUri(ReleaseSearchUrl.Build(query, page: 1, pageSize))
            .ToString();

        if (targetUri == navigationManager.Uri)
        {
            await RefreshReleasesAsync();
            return;
        }

        navigationManager.NavigateTo(targetUri);
    }

    private void ToggleReleaseSelection(int releaseId, bool selected)
    {
        if (selected)
        {
            selectedReleaseIds.Add(releaseId);
            return;
        }

        selectedReleaseIds.Remove(releaseId);
    }

    private void SelectAllVisibleReleases()
    {
        foreach (var release in releases)
        {
            selectedReleaseIds.Add(release.ReleaseId);
        }
    }

    private void DeselectAllReleases()
    {
        selectedReleaseIds.Clear();
        selectedBulkReleaseGroupId = 0;
        selectedBulkPrimaryLanguageCode = NoBulkLanguageSelected;
    }

    private async Task ApplyBulkReleaseGroupAsync()
    {
        if (selectedReleaseIds.Count == 0 || selectedBulkReleaseGroupId == 0)
        {
            return;
        }

        var releaseIds = selectedReleaseIds.ToList();

        await operationRunner.RunAsync(
            (ReleaseService service) =>
                service.UpdateReleaseGroupAsync(releaseIds, selectedBulkReleaseGroupId)
        );

        toastService.Success(L["ReleaseGroupChangedForReleases", releaseIds.Count]);
        selectedReleaseIds.Clear();
        selectedBulkReleaseGroupId = 0;
        selectedBulkPrimaryLanguageCode = NoBulkLanguageSelected;
        await RefreshReleasesAsync();
    }

    private async Task ApplyBulkPrimaryLanguageAsync()
    {
        if (
            selectedReleaseIds.Count == 0
            || selectedBulkPrimaryLanguageCode == NoBulkLanguageSelected
        )
        {
            return;
        }

        var releaseIds = selectedReleaseIds.ToList();
        await operationRunner.RunAsync(
            (ReleaseService service) =>
                service.UpdatePrimaryLanguageAsync(releaseIds, selectedBulkPrimaryLanguageCode)
        );

        toastService.Success(L["PrimaryLanguageChangedForReleases", releaseIds.Count]);
        selectedReleaseIds.Clear();
        selectedBulkReleaseGroupId = 0;
        selectedBulkPrimaryLanguageCode = NoBulkLanguageSelected;
        await RefreshReleasesAsync();
    }

    private void OnPageSizeChanged()
    {
        navigationManager.NavigateTo(ReleaseSearchUrl.Build(searchQuery, page: 1, pageSize));
    }
}
