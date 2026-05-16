using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.ManageHosters.Dto;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Dto;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Dto;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class AllReleasesPage(
    IReleaseReadRepository readRepository,
    IHosterConfigurationReadRepository hosterReadRepository,
    ILinkCrypterRegistrationReadRepository linkCrypterReadRepository,
    IReleaseGroupReadRepository releaseGroupReadRepository,
    DialogService dialogService,
    ToastService toastService
)
{
    private readonly int[] pageSizes = [5, 10, 20, 50, 100];

    private IReadOnlyList<ReleaseDto> releases = [];
    private IReadOnlyList<HosterRegistrationDto> hosterRegistrations = [];
    private IReadOnlyList<ArchiverDto> archiverOptions = [];
    private IReadOnlyList<LinkCrypterRegistrationDto> linkCrypterRegistrations = [];
    private IReadOnlyList<ReleaseGroupDto> releaseGroups = [];
    private readonly HashSet<int> selectedReleaseIds = [];
    private ReleaseService service = null!;
    private ReleaseSearchQuery searchQuery = new();
    private int totalCount;
    private int pageIndex;
    private int pageSize = 5;
    private int selectedBulkReleaseGroupId;
    private bool isLoading;

    private int CurrentPage => totalCount == 0 ? 1 : pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * pageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * pageSize);
    private string ReleasesTableKey => $"{pageIndex}-{pageSize}-{searchQuery.GetHashCode()}";
    private bool AreAllVisibleReleasesSelected =>
        releases.Count > 0 && releases.All(r => selectedReleaseIds.Contains(r.ReleaseId));

    private IEnumerable<SelectOption<int>> PageSizeOptions =>
        pageSizes.Select(size => new SelectOption<int>(size, size.ToString()));

    private IEnumerable<SelectOption<int>> ReleaseGroupOptions =>
        new[] { new SelectOption<int>(0, L["SelectReleaseGroup"]) }.Concat(
            releaseGroups.Select(group => new SelectOption<int>(group.ReleaseGroupId, group.Name))
        );

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
        service = ScopedServices.GetRequiredService<ReleaseService>();
        hosterRegistrations = await hosterReadRepository.GetAllRegistrationsAsync();
        archiverOptions = readRepository.GetArchiverFilterOptions();
        linkCrypterRegistrations = await linkCrypterReadRepository.GetAllAsync();
        releaseGroups = await releaseGroupReadRepository.GetAllAsync();
        await RefreshReleasesAsync();
    }

    private async Task DeleteReleaseAsync(ReleaseDto release)
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

        await service.DeleteAsync(release.ReleaseId);
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

    private async Task ShowEditReleaseDialogAsync(ReleaseDto release)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseDialog.ReleaseId)] = release.ReleaseId,
            [nameof(CreateOrEditReleaseDialog.FormModel)] = new ReleaseFormModel
            {
                Name = release.Name,
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
            var result = await readRepository.SearchReleasesAsync(
                searchQuery with
                {
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                }
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
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ApplySearchAsync(ReleaseSearchQuery query)
    {
        searchQuery = query;
        pageIndex = 0;
        selectedReleaseIds.Clear();
        await RefreshReleasesAsync();
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
    }

    private async Task ApplyBulkReleaseGroupAsync()
    {
        if (selectedReleaseIds.Count == 0 || selectedBulkReleaseGroupId == 0)
        {
            return;
        }

        var releaseIds = selectedReleaseIds.ToList();

        await service.UpdateReleaseGroupAsync(releaseIds, selectedBulkReleaseGroupId);

        toastService.Success(L["ReleaseGroupChangedForReleases", releaseIds.Count]);
        selectedReleaseIds.Clear();
        selectedBulkReleaseGroupId = 0;
        await RefreshReleasesAsync();
    }

    private async Task GoToPageAsync(int page)
    {
        var nextPageIndex = Math.Clamp(page - 1, 0, TotalPages - 1);
        if (nextPageIndex == pageIndex)
        {
            return;
        }

        pageIndex = nextPageIndex;
        await RefreshReleasesAsync();
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
        await RefreshReleasesAsync();
    }
}
