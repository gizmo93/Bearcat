using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Uploads;

public partial class UploadLane(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RunningUploadReadModel> Uploads { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> UploadProgress { get; set; } = null!;

    [Parameter]
    public EventCallback OnUploadCanceled { get; set; }

    private IReadOnlyList<RunningUploadReadModel> ActiveUploads =>
        Uploads.Where(upload => upload.UploadState is not UploadState.Pending).ToList();

    private IReadOnlyList<RunningUploadReadModel> QueuedUploads =>
        Uploads.Where(upload => upload.UploadState is UploadState.Pending).ToList();

    private static bool CanCancelUpload(RunningUploadReadModel upload) =>
        upload.UploadState is UploadState.Pending or UploadState.Uploading;

    private async Task CancelUploadAsync(RunningUploadReadModel upload)
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

        var cancellationRequested = await operationRunner.RunAsync(
            (UploadStateService service) => service.CancelUploadAsync(upload.UploadId)
        );

        if (cancellationRequested)
        {
            toastService.Success(L["UploadCancellationRequested", upload.UploadId]);
            await OnUploadCanceled.InvokeAsync();
        }
        else
        {
            toastService.Error(L["UploadCancellationNotAvailable", upload.UploadId]);
        }
    }
}
