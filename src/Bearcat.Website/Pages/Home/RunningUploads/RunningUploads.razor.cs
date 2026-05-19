using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningUploads;

public partial class RunningUploads(
    DialogService dialogService,
    ToastService toastService,
    UploadStateService uploadStateService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Upload> Uploads { get; set; } = null!;

    [Parameter]
    public EventCallback OnUploadCanceled { get; set; }

    private readonly HashSet<int> showDetailIds = [];

    private IEnumerable<Upload> SortedUploads => Uploads.OrderByDescending(u => u.UploadState);

    private IReadOnlyList<Upload> ExpandedUploads =>
        SortedUploads.Where(upload => showDetailIds.Contains(upload.Id)).ToList();

    private void ToggleShowUploadDetails(int uploadId)
    {
        if (!showDetailIds.Remove(uploadId))
        {
            showDetailIds.Add(uploadId);
        }

        StateHasChanged();
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

    private async Task CancelUploadAsync(Upload upload)
    {
        var result = await dialogService.ConfirmAsync(
            L["CancelUpload"],
            L["CancelUploadConfirmation", upload.Id],
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

        var cancellationRequested = await uploadStateService.CancelUploadAsync(upload.Id);

        if (cancellationRequested)
        {
            toastService.Success(L["UploadCancellationRequested", upload.Id]);
            await OnUploadCanceled.InvokeAsync();
        }
        else
        {
            toastService.Error(L["UploadCancellationNotAvailable", upload.Id]);
        }
    }

    private static bool CanCancelUpload(Upload upload) =>
        upload.UploadState is UploadState.Pending or UploadState.Uploading;

    private static double GetUploadProgress(Upload upload)
    {
        var uploadedFiles = upload.UploadedFiles.Count;
        var archiveFiles = upload.Archive?.ArchiveFiles.Count ?? 0;

        if (archiveFiles == 0)
        {
            return 0;
        }

        return Math.Round((double)uploadedFiles / archiveFiles * 100, 0);
    }
}
