using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public partial class LinkCrypterImportContainerList(
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    private const int PageSize = 25;

    private const string ContainerGridClass =
        "lg:grid-cols-[minmax(0,1.4fr)_minmax(0,0.8fr)_minmax(0,2fr)]";

    private IReadOnlyList<LinkCrypterImportContainerReadModel> containers = [];
    private LinkCrypterImportDetailReadModel? loadedDetail;
    private LinkCrypterImportContainerStatus? selectedStatus;
    private int totalCount;
    private int pageIndex;

    [Parameter]
    [EditorRequired]
    public LinkCrypterImportDetailReadModel Detail { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyDictionary<string, string> HosterNamesByClassName { get; set; } = null!;

    [Parameter]
    public EventCallback OnSelectionChanged { get; set; }

    private bool CanChangeSelection => Detail.State is LinkCrypterImportState.ReadyForReview;

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
        await LoadContainersAsync();
    }

    private async Task LoadContainersAsync()
    {
        var result = await operationRunner.RunAsync(
            (ILinkCrypterImportReadRepository repository) =>
                repository.SearchContainersAsync(
                    new LinkCrypterImportContainerSearchQuery(
                        LinkCrypterImportId: Detail.Id,
                        Statuses: selectedStatus is { } status ? [status] : [],
                        PageIndex: pageIndex,
                        PageSize: PageSize
                    )
                )
        );

        containers = result.Items;
        totalCount = result.TotalCount;
        pageIndex = result.PageIndex;
    }

    private async Task SelectStatusAsync(LinkCrypterImportContainerStatus? status)
    {
        selectedStatus = status;
        pageIndex = 0;
        await LoadContainersAsync();
    }

    private async Task GoToPageAsync(int page)
    {
        pageIndex = page - 1;
        await LoadContainersAsync();
    }

    private async Task SetSelectionAsync(
        LinkCrypterImportContainerReadModel container,
        bool isSelected
    )
    {
        try
        {
            await operationRunner.RunAsync(
                (LinkCrypterImportService service) =>
                    service.SetContainerSelectionAsync(Detail.Id, [container.Id], isSelected)
            );
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
        }

        await OnSelectionChanged.InvokeAsync();
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

        await OnSelectionChanged.InvokeAsync();
    }

    private int GetImportableContainerCount()
    {
        return Detail
            .ContainerCountsByStatus.Where(count =>
                count.Status is LinkCrypterImportContainerStatus.Importable
            )
            .Sum(count => count.Count);
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
