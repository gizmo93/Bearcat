using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Formatting;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningUploads;

public partial class RunningUploads(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner,
    ITransferCancellationRegistry transferCancellationRegistry
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RunningUploadReadModel> Uploads { get; set; } = null!;

    [Parameter]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> UploadProgress { get; set; } =
        new Dictionary<int, TransferProgressSnapshot>();

    [Parameter]
    public IReadOnlyList<RunningArchiveReadModel> RestoringArchives { get; set; } = [];

    [Parameter]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> DownloadProgress { get; set; } =
        new Dictionary<int, TransferProgressSnapshot>();

    [Parameter]
    public EventCallback OnUploadCanceled { get; set; }

    private readonly HashSet<int> showDetailIds = [];

    private readonly HashSet<int> showDownloadDetailIds = [];

    private IEnumerable<RunningUploadReadModel> SortedUploads =>
        Uploads.OrderByDescending(u => u.UploadState);

    private IReadOnlyList<RunningUploadReadModel> ExpandedUploads =>
        SortedUploads.Where(upload => showDetailIds.Contains(upload.UploadId)).ToList();

    private IReadOnlyList<RunningArchiveReadModel> ExpandedRestores =>
        RestoringArchives
            .Where(archive =>
                showDownloadDetailIds.Contains(archive.ArchiveId)
                && DownloadProgress.ContainsKey(archive.ArchiveId)
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

    private static bool CanCancelUpload(RunningUploadReadModel upload) =>
        upload.UploadState is UploadState.Pending or UploadState.Uploading;

    private async Task CancelDownloadAsync(RunningArchiveReadModel archive)
    {
        var result = await dialogService.ConfirmAsync(
            L["CancelDownloadTitle"],
            L["CancelDownloadConfirmation", archive.ArchiveId],
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

        if (
            transferCancellationRegistry.RequestCancellation(
                new TransferIdentifier(TransferType.MirrorDownload, archive.ArchiveId)
            )
        )
        {
            toastService.Success(L["DownloadCancellationRequested", archive.ArchiveId]);
            return;
        }

        toastService.Error(L["DownloadAlreadyFinished", archive.ArchiveId]);
    }

    private double GetUploadProgress(RunningUploadReadModel upload)
    {
        return UploadProgress.TryGetValue(upload.UploadId, out var snapshot)
            ? snapshot.Percentage
            : 0;
    }

    private double GetDownloadProgress(RunningArchiveReadModel archive)
    {
        return DownloadProgress.TryGetValue(archive.ArchiveId, out var snapshot)
            ? snapshot.Percentage
            : 0;
    }

    private TransferProgressSnapshot? GetDownloadSnapshot(int archiveId)
    {
        return DownloadProgress.GetValueOrDefault(archiveId);
    }

    private string GetDownloadHosterName(RunningArchiveReadModel archive)
    {
        return DownloadProgress.TryGetValue(archive.ArchiveId, out var snapshot)
            ? snapshot.SourceName
            : "-";
    }

    private IReadOnlyList<string> GetUploadProxyServerNames(int uploadId)
    {
        return UploadProgress.TryGetValue(uploadId, out var snapshot)
            ? snapshot.ProxyServerNames
            : [];
    }

    private IReadOnlyList<string> GetDownloadProxyServerNames(int archiveId)
    {
        return DownloadProgress.TryGetValue(archiveId, out var snapshot)
            ? snapshot.ProxyServerNames
            : [];
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
            ? TransferFormatting.FormatSpeed(snapshot.BytesPerSecond)
            : null;
    }

    private string? FormatDownloadSpeed(int archiveId)
    {
        return DownloadProgress.TryGetValue(archiveId, out var snapshot)
            ? TransferFormatting.FormatSpeed(snapshot.BytesPerSecond)
            : null;
    }
}
