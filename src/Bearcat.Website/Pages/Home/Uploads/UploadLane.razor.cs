using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.Exceptions;
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
    public EventCallback OnUploadChanged { get; set; }

    private IReadOnlyList<RunningUploadReadModel> ActiveUploads =>
        Uploads.Where(upload => upload.UploadState is not UploadState.Pending).ToList();

    private IReadOnlyList<RunningUploadReadModel> QueuedUploads =>
        Uploads.Where(upload => upload.UploadState is UploadState.Pending).ToList();

    private static bool CanCancelUpload(RunningUploadReadModel upload) =>
        upload.UploadState is UploadState.Pending or UploadState.Uploading;

    private static bool CanBePrioritized(RunningUploadReadModel upload) =>
        upload.UploadState is UploadState.Pending or UploadState.Uploading;

    private static bool IsPrioritizationShown(RunningUploadReadModel upload) =>
        upload.PrioritizedAt is not null && CanBePrioritized(upload);

    private async Task ToggleUploadPrioritizationAsync(RunningUploadReadModel upload)
    {
        if (upload.PrioritizedAt is null)
        {
            await PrioritizeUploadAsync(upload);
        }
        else
        {
            await RemoveUploadPrioritizationAsync(upload);
        }

        await OnUploadChanged.InvokeAsync();
    }

    private async Task PrioritizeUploadAsync(RunningUploadReadModel upload)
    {
        try
        {
            await operationRunner.RunAsync(
                (UploadStateService service) => service.PrioritizeUploadAsync(upload.UploadId)
            );

            toastService.Success(L["UploadPrioritized", upload.UploadId]);
        }
        catch (InvalidUploadStateException)
        {
            toastService.Error(L["UploadPrioritizationNotAvailable", upload.UploadId]);
        }
    }

    private async Task RemoveUploadPrioritizationAsync(RunningUploadReadModel upload)
    {
        await operationRunner.RunAsync(
            (UploadStateService service) => service.RemoveUploadPrioritizationAsync(upload.UploadId)
        );

        toastService.Success(L["UploadPrioritizationRemoved", upload.UploadId]);
    }

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
            await OnUploadChanged.InvokeAsync();
        }
        else
        {
            toastService.Error(L["UploadCancellationNotAvailable", upload.UploadId]);
        }
    }
}
