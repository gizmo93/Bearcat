using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseUploads(
    DialogService dialogService,
    IServiceScopeFactory serviceScopeFactory,
    ToastService toastService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? InitialUploadConfigId { get; set; }

    private readonly int[] pageSizes = [5, 10, 20, 50, 100];
    private IReadOnlyList<ReleaseUploadReadModel> uploads = [];
    private IReadOnlyList<UploadConfigReadModel> uploadConfigs = [];
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
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var uploadConfigReadRepository =
            scope.ServiceProvider.GetRequiredService<IUploadConfigReadRepository>();
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
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var readRepository = scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();
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

    private async Task ShowLinksDialogAsync(ReleaseUploadReadModel upload)
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

    private async Task ShowContainerLinksDialogAsync(ReleaseUploadReadModel upload)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(UploadContainerLinksDialog.ReleaseId)] = ReleaseId,
            [nameof(UploadContainerLinksDialog.UploadId)] = upload.UploadId,
            [nameof(UploadContainerLinksDialog.UploadConfigName)] = upload.UploadConfigName,
        };

        await dialogService.OpenAsync<UploadContainerLinksDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["ContainerLinksTitle", upload.UploadId],
                Description = L["ContainerLinksDialogDescription", upload.UploadConfigName],
                Size = DialogSize.ExtraLarge,
                ShowClose = true,
            }
        );
    }

    private async Task CreateManualReuploadAsync(ReleaseUploadReadModel upload)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var uploadStateService = scope.ServiceProvider.GetRequiredService<UploadStateService>();
        await uploadStateService.CreateManualReuploadAsync(upload.UploadId);
        toastService.Success(L["ManualReuploadCreated", upload.UploadId]);
        await RefreshUploadsAsync();
    }

    private async Task CancelUploadAsync(ReleaseUploadReadModel upload)
    {
        var result = await dialogService.ConfirmAsync(
            L["CancelUpload"],
            L["CancelUploadConfirmation", upload.UploadId],
            new ConfirmDialogOptions
            {
                ConfirmText = L["CancelUpload"],
                CancelText = L["Close"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var uploadStateService = scope.ServiceProvider.GetRequiredService<UploadStateService>();
        var cancellationRequested = await uploadStateService.CancelUploadAsync(upload.UploadId);

        if (cancellationRequested)
        {
            toastService.Success(L["UploadCancellationRequested", upload.UploadId]);
        }
        else
        {
            toastService.Error(L["UploadCancellationNotAvailable", upload.UploadId]);
        }

        await RefreshUploadsAsync();
    }

    private async Task ResumeUploadAsync(ReleaseUploadReadModel upload)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var uploadStateService = scope.ServiceProvider.GetRequiredService<UploadStateService>();
        var resumed = await uploadStateService.ResumeUploadAsync(upload.UploadId);

        if (resumed)
        {
            toastService.Success(L["UploadResumed", upload.UploadId]);
        }
        else
        {
            toastService.Error(L["UploadResumeNotAvailable", upload.UploadId]);
        }

        await RefreshUploadsAsync();
    }

    private async Task DeleteUploadAsync(ReleaseUploadReadModel upload)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteUpload"],
            L["DeleteUploadConfirmation", upload.UploadId],
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

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var uploadStateService = scope.ServiceProvider.GetRequiredService<UploadStateService>();
        var deleted = await uploadStateService.DeleteUploadAsync(upload.UploadId);

        if (deleted)
        {
            toastService.Success(L["UploadDeleted", upload.UploadId]);
        }
        else
        {
            toastService.Error(L["UploadDeleteNotAvailable", upload.UploadId]);
        }

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
            UploadState.CancellationRequested => BadgeVariant.Secondary,
            UploadState.Pending => BadgeVariant.Secondary,
            UploadState.Failed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private static bool CanCreateManualReupload(ReleaseUploadReadModel upload) =>
        upload.CanCreateReupload;

    private static bool CanCancelUpload(ReleaseUploadReadModel upload) =>
        upload.UploadState is UploadState.Pending or UploadState.Uploading;

    private static bool CanResumeUpload(ReleaseUploadReadModel upload) =>
        upload.UploadState is UploadState.Canceled;

    private static bool CanDeleteUpload(ReleaseUploadReadModel upload) =>
        upload.UploadState
            is UploadState.Pending
                or UploadState.Completed
                or UploadState.Failed
                or UploadState.Canceled;
}
