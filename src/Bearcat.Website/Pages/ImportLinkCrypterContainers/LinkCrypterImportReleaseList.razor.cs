using System.Globalization;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleases;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public partial class LinkCrypterImportReleaseList(
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    private const int PageSize = 25;

    private const string ReleaseGridClass =
        "lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1.4fr)_minmax(0,1fr)]";

    private IReadOnlyList<LinkCrypterImportReleaseReadModel> releases = [];
    private IReadOnlyList<LinkCrypterImportSelectableContainerReadModel> selectableContainers = [];
    private LinkCrypterImportDetailReadModel? loadedDetail;
    private LinkCrypterImportContainerStatus? selectedStatus;
    private string? searchText;
    private string? releaseNameBeingEdited;
    private string editedReleaseName = "";
    private bool isRenaming;
    private bool isSettingReleaseTemplate;
    private int totalCount;
    private int pageIndex;

    [Parameter]
    [EditorRequired]
    public LinkCrypterImportDetailReadModel Detail { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyDictionary<string, string> HosterNamesByClassName { get; set; } = null!;

    [Parameter]
    public EventCallback OnReleasesChanged { get; set; }

    private bool CanChangeReleases => Detail.State is LinkCrypterImportState.ReadyForReview;

    private bool CanChangeSelection => CanChangeReleases && selectableContainers.Count > 0;

    private bool IsFilterActive =>
        selectedStatus is not null || !string.IsNullOrWhiteSpace(searchText);

    private bool AreAllSelectableContainersSelected =>
        selectableContainers.All(container => container.IsSelected);

    private bool AreSomeSelectableContainersSelected =>
        !AreAllSelectableContainersSelected
        && selectableContainers.Any(container => container.IsSelected);

    private int SelectedReleaseCount =>
        GetSelectedContainers()
            .Select(container => container.ReleaseName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    private IReadOnlyList<SelectOption<int>> ReleaseTemplateOptions =>
        [
            new(Detail.ReleaseTemplateId, Detail.ReleaseTemplateName),
            new(Detail.SeriesReleaseTemplateId!.Value, Detail.SeriesReleaseTemplateName!),
        ];

    private IReadOnlyList<SelectOption<string>> PrimaryLanguageOptions =>
        PrimaryLanguageSelectOptions.Create(L["NotSet"]);

    private int CurrentPage => pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * PageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * PageSize);

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(loadedDetail, Detail))
        {
            return;
        }

        loadedDetail = Detail;
        await LoadReleasesAsync();
    }

    private async Task LoadReleasesAsync()
    {
        var filter = new LinkCrypterImportReleaseFilter(
            LinkCrypterImportId: Detail.Id,
            Statuses: selectedStatus is { } status ? [status] : [],
            SearchText: searchText
        );

        var result = await operationRunner.RunAsync(
            (ILinkCrypterImportReadRepository repository) =>
                repository.SearchReleasesAsync(
                    new LinkCrypterImportReleaseSearchQuery(
                        Filter: filter,
                        PageIndex: pageIndex,
                        PageSize: PageSize
                    )
                )
        );

        selectableContainers = await operationRunner.RunAsync(
            (ILinkCrypterImportReadRepository repository) =>
                repository.GetSelectableContainersOfFilteredReleasesAsync(filter)
        );
        releases = result.Items;
        totalCount = result.TotalCount;
        pageIndex = result.PageIndex;
    }

    private async Task SelectStatusAsync(LinkCrypterImportContainerStatus? status)
    {
        selectedStatus = status;
        pageIndex = 0;
        await LoadReleasesAsync();
    }

    private async Task SearchAsync(string? text)
    {
        searchText = text;
        pageIndex = 0;
        await LoadReleasesAsync();
    }

    private async Task GoToPageAsync(int page)
    {
        pageIndex = page - 1;
        await LoadReleasesAsync();
    }

    private void StartRenaming(LinkCrypterImportReleaseReadModel release)
    {
        releaseNameBeingEdited = release.ReleaseName;
        editedReleaseName = release.ReleaseName;
    }

    private void CancelRenaming()
    {
        releaseNameBeingEdited = null;
        editedReleaseName = "";
    }

    private async Task RenameReleaseAsync()
    {
        var currentReleaseName = releaseNameBeingEdited!;
        isRenaming = true;

        try
        {
            await operationRunner.RunAsync(
                (LinkCrypterImportService service) =>
                    service.RenameReleaseAsync(Detail.Id, currentReleaseName, editedReleaseName)
            );
            CancelRenaming();
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
            return;
        }
        finally
        {
            isRenaming = false;
        }

        await OnReleasesChanged.InvokeAsync();
    }

    private async Task SetReleaseSelectionAsync(
        LinkCrypterImportReleaseReadModel release,
        bool isSelected
    )
    {
        try
        {
            await operationRunner.RunAsync(
                (LinkCrypterImportService service) =>
                    service.SetReleaseSelectionAsync(Detail.Id, release.ReleaseName, isSelected)
            );
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
        }

        await OnReleasesChanged.InvokeAsync();
    }

    private async Task SetSelectionOfAllSelectableContainersAsync(bool isSelected)
    {
        var containerIds = selectableContainers.Select(container => container.Id).ToList();

        try
        {
            await operationRunner.RunAsync(
                (LinkCrypterImportService service) =>
                    service.SetSelectionOfContainersAsync(Detail.Id, containerIds, isSelected)
            );
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
        }

        await OnReleasesChanged.InvokeAsync();
    }

    private void StartSettingReleaseTemplate(int releaseTemplateId)
    {
        isSettingReleaseTemplate = true;
        _ = SetReleaseTemplateInBackgroundAsync(
            containerIds: GetSelectedContainers().Select(container => container.Id).ToList(),
            releaseCount: SelectedReleaseCount,
            releaseTemplateId: releaseTemplateId
        );
    }

    private async Task SetReleaseTemplateInBackgroundAsync(
        IReadOnlyList<int> containerIds,
        int releaseCount,
        int releaseTemplateId
    )
    {
        try
        {
            await operationRunner.RunAsync(
                (LinkCrypterImportService service) =>
                    service.SetReleaseTemplateAsync(Detail.Id, containerIds, releaseTemplateId)
            );

            toastService.Success(L["ReleaseTemplateChangedForReleases", releaseCount]);
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);
        }
        finally
        {
            isSettingReleaseTemplate = false;
        }

        await OnReleasesChanged.InvokeAsync();
        StateHasChanged();
    }

    private async Task SetPrimaryLanguageAsync(string languageCode)
    {
        var containerIds = GetSelectedContainers().Select(container => container.Id).ToList();
        var releaseCount = SelectedReleaseCount;

        try
        {
            await operationRunner.RunAsync(
                (LinkCrypterImportService service) =>
                    service.SetPrimaryLanguageAsync(Detail.Id, containerIds, languageCode)
            );

            toastService.Success(L["PrimaryLanguageChangedForReleases", releaseCount]);
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
        }

        await OnReleasesChanged.InvokeAsync();
    }

    private List<LinkCrypterImportSelectableContainerReadModel> GetSelectedContainers()
    {
        return selectableContainers.Where(container => container.IsSelected).ToList();
    }

    private static bool IsReleasePartlySelected(LinkCrypterImportReleaseReadModel release)
    {
        return !release.IsSelected
            && release.Containers.Any(container => container.IsSelectable && container.IsSelected);
    }

    private static IReadOnlyList<LinkCrypterImportMirror> GetMirrorsOfSingleRelease(
        LinkCrypterImportContainerPreview preview
    )
    {
        return preview.Releases is { Count: 1 } releasePreviews
            ? releasePreviews.Single().Mirrors
            : preview.Mirrors;
    }

    private static List<LinkCrypterImportMirror> GetMirrorsWithoutRelease(
        LinkCrypterImportContainerPreview preview
    )
    {
        var mirrorNumbersOfReleases = preview
            .Releases!.SelectMany(releasePreview => releasePreview.Mirrors)
            .Select(mirror => mirror.MirrorNumber)
            .ToHashSet();

        return preview
            .Mirrors.Where(mirror => !mirrorNumbersOfReleases.Contains(mirror.MirrorNumber))
            .ToList();
    }

    private string GetLocalFilesText(LinkCrypterImportReleaseReadModel release)
    {
        return LinkCrypterImportLocalFilesText.Get(
            localizer: L,
            releaseAction: release.ReleaseAction,
            releaseFolderPath: release.ReleaseFolderPath,
            archiveFolderPath: release.ArchiveFolderPath
        );
    }

    private string GetLocalFilesText(
        LinkCrypterImportContainerReadModel container,
        LinkCrypterImportReleasePreview releasePreview
    )
    {
        return LinkCrypterImportLocalFilesText.Get(
            localizer: L,
            releasePreview: releasePreview,
            archivePartsWereMoved: container.Status is LinkCrypterImportContainerStatus.Imported
        );
    }

    private string GetLanguageDisplayName(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return L["NotSet"];
        }

        var culture = CultureInfo.GetCultureInfo(languageCode);
        return $"{culture.NativeName} ({culture.TwoLetterISOLanguageName})";
    }

    private string GetHosterName(string? hosterClassName)
    {
        return hosterClassName is null
            ? L["UnknownHoster"]
            : HosterNamesByClassName.GetValueOrDefault(hosterClassName, hosterClassName);
    }

    private static BadgeVariant GetStatusBadgeVariant(LinkCrypterImportContainerStatus status)
    {
        return status switch
        {
            LinkCrypterImportContainerStatus.ReadingLinksFailed
            or LinkCrypterImportContainerStatus.ImportFailed => BadgeVariant.Destructive,
            LinkCrypterImportContainerStatus.Imported
            or LinkCrypterImportContainerStatus.AlreadyInBearcat => BadgeVariant.Secondary,
            _ => BadgeVariant.Outline,
        };
    }
}
