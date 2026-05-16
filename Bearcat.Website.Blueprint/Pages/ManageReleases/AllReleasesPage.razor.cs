using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.ManageHosters.Dto;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Dto;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Blueprint.Localization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint.Pages.ManageReleases;

public partial class AllReleasesPage(
    IReleaseReadRepository readRepository,
    IHosterConfigurationReadRepository hosterReadRepository,
    ILinkCrypterRegistrationReadRepository linkCrypterReadRepository,
    DialogService dialogService
)
{
    private readonly int[] pageSizes = [10, 20, 50, 100];

    private IReadOnlyList<ReleaseDto> releases = [];
    private IReadOnlyList<HosterRegistrationDto> hosterRegistrations = [];
    private IReadOnlyList<ArchiverDto> archiverOptions = [];
    private IReadOnlyList<LinkCrypterRegistrationDto> linkCrypterRegistrations = [];
    private ReleaseService service = null!;
    private string? searchTerm;
    private string? linksDistributedTo;
    private OnlineState? selectedOnlineState;
    private int? selectedHosterRegistrationId;
    private string selectedArchiverName = string.Empty;
    private int? selectedLinkCrypterRegistrationId;
    private int totalCount;
    private int pageIndex;
    private int pageSize = 10;
    private bool isLoading;

    private int CurrentPage => totalCount == 0 ? 1 : pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * pageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * pageSize);
    private bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(searchTerm)
        || !string.IsNullOrWhiteSpace(linksDistributedTo)
        || selectedOnlineState is not null
        || selectedHosterRegistrationId is not null
        || !string.IsNullOrWhiteSpace(selectedArchiverName)
        || selectedLinkCrypterRegistrationId is not null;

    private IEnumerable<SelectOption<OnlineState?>> OnlineStateOptions =>
        new SelectOption<OnlineState?>[]
        {
            new(null, L["AnyOnlineState"]),
            new(OnlineState.Online, L.Localize(OnlineState.Online)),
            new(OnlineState.PartiallyOnline, L.Localize(OnlineState.PartiallyOnline)),
            new(OnlineState.Offline, L.Localize(OnlineState.Offline)),
            new(OnlineState.Unknown, L.Localize(OnlineState.Unknown)),
        };

    private IEnumerable<SelectOption<int?>> HosterRegistrationOptions =>
        new[] { new SelectOption<int?>(null, L["AnyHosterConfig"]) }.Concat(
            hosterRegistrations.Select(h => new SelectOption<int?>(
                h.Id,
                $"{h.Name} ({h.HosterName})"
            ))
        );

    private IEnumerable<SelectOption<string>> ArchiverOptions =>
        new[] { new SelectOption<string>(string.Empty, L["AnyArchiver"]) }.Concat(
            archiverOptions.Select(a => new SelectOption<string>(
                a.ClassName,
                $"{a.Name} ({a.FileExtension})"
            ))
        );

    private IEnumerable<SelectOption<int?>> LinkCrypterRegistrationOptions =>
        new[] { new SelectOption<int?>(null, L["AnyLinkCrypterConfig"]) }.Concat(
            linkCrypterRegistrations.Select(l => new SelectOption<int?>(
                l.LinkCrypterRegistrationId,
                $"{l.Name} ({l.CrypterName})"
            ))
        );

    private IEnumerable<SelectOption<int>> PageSizeOptions =>
        pageSizes.Select(size => new SelectOption<int>(size, size.ToString()));

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

    private async Task RefreshReleasesAsync()
    {
        isLoading = true;

        try
        {
            var result = await readRepository.SearchReleasesAsync(
                new ReleaseSearchQuery(
                    SearchTerm: searchTerm,
                    OnlineState: selectedOnlineState,
                    HosterRegistrationId: selectedHosterRegistrationId,
                    ArchiverName: selectedArchiverName,
                    LinkCrypterRegistrationId: selectedLinkCrypterRegistrationId,
                    LinksDistributedTo: linksDistributedTo,
                    PageIndex: pageIndex,
                    PageSize: pageSize
                )
            );

            releases = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;

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

    private async Task ApplyFiltersAsync()
    {
        pageIndex = 0;
        await RefreshReleasesAsync();
    }

    private async Task ResetFiltersAsync()
    {
        searchTerm = null;
        linksDistributedTo = null;
        selectedOnlineState = null;
        selectedHosterRegistrationId = null;
        selectedArchiverName = string.Empty;
        selectedLinkCrypterRegistrationId = null;
        pageIndex = 0;
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
