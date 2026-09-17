using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Cancellation;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.Progress;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Humanizer;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningUploads;

public partial class RunningUploads(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner,
    IDownloadCancellationRegistry downloadCancellationRegistry
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Upload> Uploads { get; set; } = null!;

    [Parameter]
    public IReadOnlyDictionary<int, UploadProgressSnapshot> UploadProgress { get; set; } =
        new Dictionary<int, UploadProgressSnapshot>();

    [Parameter]
    public IReadOnlyList<Archive> RestoringArchives { get; set; } = [];

    [Parameter]
    public IReadOnlyDictionary<int, DownloadProgressSnapshot> DownloadProgress { get; set; } =
        new Dictionary<int, DownloadProgressSnapshot>();

    [Parameter]
    public EventCallback OnUploadCanceled { get; set; }

    private readonly HashSet<int> showDetailIds = [];

    private readonly HashSet<int> showDownloadDetailIds = [];

    private IEnumerable<Upload> SortedUploads => Uploads.OrderByDescending(u => u.UploadState);

    private IReadOnlyList<Upload> ExpandedUploads =>
        SortedUploads.Where(upload => showDetailIds.Contains(upload.Id)).ToList();

    private IReadOnlyList<Archive> ExpandedRestores =>
        RestoringArchives
            .Where(archive =>
                showDownloadDetailIds.Contains(archive.Id)
                && DownloadProgress.ContainsKey(archive.Id)
            )
            .ToList();

    private void ToggleShowUploadDetails(int uploadId)
    {
        if (!showDetailIds.Remove(uploadId))
        {
            showDetailIds.Add(uploadId);
        }

        StateHasChanged();
    }

    private void ToggleShowDownloadDetails(int archiveId)
    {
        if (!showDownloadDetailIds.Remove(archiveId))
        {
            showDownloadDetailIds.Add(archiveId);
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

        var cancellationRequested = await operationRunner.RunAsync(
            (UploadStateService service) => service.CancelUploadAsync(upload.Id)
        );

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

    private async Task CancelDownloadAsync(Archive archive)
    {
        var result = await dialogService.ConfirmAsync(
            L["CancelDownloadTitle"],
            L["CancelDownloadConfirmation", archive.Id],
            new ConfirmDialogOptions
            {
                ConfirmText = L["CancelDownload"],
                CancelText = L["Close"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        if (downloadCancellationRegistry.RequestCancellation(archive.Id))
        {
            toastService.Success(L["DownloadCancellationRequested", archive.Id]);
            return;
        }

        toastService.Error(L["DownloadAlreadyFinished", archive.Id]);
    }

    private double GetUploadProgress(Upload upload)
    {
        return UploadProgress.TryGetValue(upload.Id, out var snapshot) ? snapshot.Percentage : 0;
    }

    private double GetDownloadProgress(Archive archive)
    {
        return DownloadProgress.TryGetValue(archive.Id, out var snapshot) ? snapshot.Percentage : 0;
    }

    private DownloadProgressSnapshot? GetDownloadSnapshot(int archiveId)
    {
        return DownloadProgress.GetValueOrDefault(archiveId);
    }

    private string GetDownloadHosterName(Archive archive)
    {
        return DownloadProgress.TryGetValue(archive.Id, out var snapshot)
            ? snapshot.HosterName
            : "-";
    }

    private double TotalUploadBytesPerSecond =>
        UploadProgress
            .Values.Select(snapshot => snapshot.BytesPerSecond)
            .Where(speed => speed > 0)
            .Sum();

    private double TotalDownloadBytesPerSecond =>
        DownloadProgress
            .Values.Select(snapshot => snapshot.BytesPerSecond)
            .Where(speed => speed > 0)
            .Sum();

    private string? FormatUploadSpeed(int uploadId)
    {
        return UploadProgress.TryGetValue(uploadId, out var snapshot)
            ? FormatSpeed(snapshot.BytesPerSecond)
            : null;
    }

    private string? FormatDownloadSpeed(int archiveId)
    {
        return DownloadProgress.TryGetValue(archiveId, out var snapshot)
            ? FormatSpeed(snapshot.BytesPerSecond)
            : null;
    }

    private static string? FormatSpeed(double bytesPerSecond)
    {
        return bytesPerSecond <= 0 ? null : $"{bytesPerSecond.Bytes().Humanize("0.0")}/s";
    }
}
