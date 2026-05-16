using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseUploads(
    IReleaseReadRepository readRepository,
    IUploadConfigReadRepository uploadConfigReadRepository,
    DialogService dialogService,
    UploadStateService uploadStateService,
    ToastService toastService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? InitialUploadConfigId { get; set; }

    private readonly int[] pageSizes = [5, 10, 20, 50, 100];
    private IReadOnlyList<ReleaseUploadDto> uploads = [];
    private IReadOnlyList<UploadConfigDto> uploadConfigs = [];
    private int totalCount;
    private int pageIndex;
    private int pageSize = 5;
    private int selectedUploadConfigId;
    private int? appliedInitialUploadConfigId;
    private bool isLoading;

    private int CurrentPage => totalCount == 0 ? 1 : pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * pageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * pageSize);
    private string UploadsTableKey => $"{selectedUploadConfigId}-{pageIndex}-{pageSize}";

    private IEnumerable<SelectOption<int>> PageSizeOptions =>
        pageSizes.Select(size => new SelectOption<int>(size, size.ToString()));

    private IEnumerable<SelectOption<int>> UploadConfigOptions =>
        new[] { new SelectOption<int>(0, L["AllUploadConfigs"]) }.Concat(
            uploadConfigs.Select(config => new SelectOption<int>(
                config.UploadConfigId,
                config.Name
            ))
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
        uploadConfigs = await uploadConfigReadRepository.GetUploadConfigsAsync(ReleaseId);
        ApplyInitialUploadConfigFilter();
        await RefreshUploadsAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (uploadConfigs.Count == 0)
        {
            return;
        }

        if (appliedInitialUploadConfigId == InitialUploadConfigId)
        {
            return;
        }

        ApplyInitialUploadConfigFilter();

        if (uploadConfigs.Count > 0)
        {
            pageIndex = 0;
            await RefreshUploadsAsync();
        }
    }

    private async Task RefreshUploadsAsync()
    {
        isLoading = true;

        try
        {
            var result = await readRepository.SearchUploadsAsync(
                new ReleaseUploadSearchQuery(
                    ReleaseId,
                    selectedUploadConfigId == 0 ? null : selectedUploadConfigId,
                    pageIndex,
                    pageSize
                )
            );

            uploads = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshUploadsAsync();
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ShowLinksDialogAsync(ReleaseUploadDto upload)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(UploadLinksDialog.ReleaseId)] = ReleaseId,
            [nameof(UploadLinksDialog.UploadId)] = upload.UploadId,
            [nameof(UploadLinksDialog.UploadConfigName)] = upload.UploadConfigName,
        };

        await dialogService.OpenAsync<UploadLinksDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["UploadLinksTitle", upload.UploadId],
                Description = L["UploadLinksDialogDescription", upload.UploadConfigName],
                Size = DialogSize.Full,
                ShowClose = true,
            }
        );
    }

    private async Task CreateManualReuploadAsync(ReleaseUploadDto upload)
    {
        await uploadStateService.CreateManualReuploadAsync(upload.UploadId);
        toastService.Success(L["ManualReuploadCreated", upload.UploadId]);
        await RefreshUploadsAsync();
    }

    private async Task GoToPageAsync(int page)
    {
        var nextPageIndex = Math.Clamp(page - 1, 0, TotalPages - 1);
        if (nextPageIndex == pageIndex)
        {
            return;
        }

        pageIndex = nextPageIndex;
        await RefreshUploadsAsync();
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
        await RefreshUploadsAsync();
    }

    private async Task OnUploadConfigFilterChangedAsync()
    {
        pageIndex = 0;
        await RefreshUploadsAsync();
    }

    private void ApplyInitialUploadConfigFilter()
    {
        appliedInitialUploadConfigId = InitialUploadConfigId;
        selectedUploadConfigId = InitialUploadConfigId ?? 0;
    }

    private static BadgeVariant GetUploadVariant(UploadState state) =>
        state switch
        {
            UploadState.Uploading => BadgeVariant.Default,
            UploadState.Pending => BadgeVariant.Secondary,
            UploadState.Failed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private static bool CanCreateManualReupload(ReleaseUploadDto upload) =>
        upload.CanCreateReupload;
}
