using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public partial class LinkCrypterImportReleaseList(
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    private const int PageSize = 25;

    private const string ReleaseGridClass =
        "lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1.4fr)]";

    private IReadOnlyList<LinkCrypterImportReleaseReadModel> releases = [];
    private LinkCrypterImportDetailReadModel? loadedDetail;
    private LinkCrypterImportContainerStatus? selectedStatus;
    private string? releaseNameBeingEdited;
    private string editedReleaseName = "";
    private bool isRenaming;
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

    private int ImportableContainerCount => GetImportableContainerCount();

    private bool AreAllImportableContainersSelected =>
        Detail.SelectedContainerCount == ImportableContainerCount;

    private bool AreSomeImportableContainersSelected =>
        Detail.SelectedContainerCount > 0 && !AreAllImportableContainersSelected;

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
        var result = await operationRunner.RunAsync(
            (ILinkCrypterImportReadRepository repository) =>
                repository.SearchReleasesAsync(
                    new LinkCrypterImportReleaseSearchQuery(
                        LinkCrypterImportId: Detail.Id,
                        Statuses: selectedStatus is { } status ? [status] : [],
                        PageIndex: pageIndex,
                        PageSize: PageSize
                    )
                )
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

    private async Task SetSelectionOfAllImportableContainersAsync(bool isSelected)
    {
        try
        {
            await operationRunner.RunAsync(
                (LinkCrypterImportService service) =>
                    service.SetSelectionOfAllImportableContainersAsync(Detail.Id, isSelected)
            );
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
        }

        await OnReleasesChanged.InvokeAsync();
    }

    private static bool IsReleasePartlySelected(LinkCrypterImportReleaseReadModel release)
    {
        return !release.IsSelected
            && release.Containers.Any(container =>
                container.Status is LinkCrypterImportContainerStatus.Importable
                && container.IsSelected
            );
    }

    private int GetImportableContainerCount()
    {
        return Detail
            .ContainerCountsByStatus.Where(count =>
                count.Status is LinkCrypterImportContainerStatus.Importable
            )
            .Sum(count => count.Count);
    }

    private string GetLocalFilesText(LinkCrypterImportReleaseReadModel release)
    {
        if (release.ReleaseAction is not LinkCrypterImportReleaseAction.CreateRelease)
        {
            return "-";
        }

        List<string> localFolderTexts = [];

        if (release.ReleaseFolderPath is { } releaseFolderPath)
        {
            localFolderTexts.Add(L["LinkCrypterImportReleaseFolder", releaseFolderPath]);
        }

        if (release.ArchiveFolderPath is { } archiveFolderPath)
        {
            localFolderTexts.Add(L["LinkCrypterImportArchiveFolder", archiveFolderPath]);
        }

        return localFolderTexts.Count == 0
            ? L["LinkCrypterImportNoLocalFiles"]
            : string.Join("; ", localFolderTexts);
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
