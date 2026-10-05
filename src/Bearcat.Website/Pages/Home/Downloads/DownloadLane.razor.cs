using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;
using Bearcat.Website.Pages.Home.Activity;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Downloads;

public partial class DownloadLane(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner,
    ITransferCancellationRegistry transferCancellationRegistry
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RunningArchiveReadModel> RestoringArchives { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> MirrorDownloadProgress { get; set; } =
        null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RemoteSourceDownloadReadModel> RemoteDownloads { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> RemoteDownloadProgress { get; set; } =
        null!;

    [Parameter]
    public EventCallback OnRemoteDownloadCanceled { get; set; }

    private IReadOnlyList<TransferPhase> RemoteDownloadPhases =>
        [
            new(TransferType.RemoteDownload, L["RemoteDownloadPhaseDownload"]),
            new(TransferType.RemoteDownloadVerification, L["RemoteDownloadPhaseVerify"]),
            new(TransferType.RemoteDownloadExtraction, L["RemoteDownloadPhaseExtract"]),
        ];

    private EventCallback GetRemoteDownloadCancelCallback(RemoteSourceDownloadReadModel download)
    {
        return download.CanCancel
            ? EventCallback.Factory.Create(this, () => CancelRemoteDownloadAsync(download))
            : default;
    }

    private async Task CancelMirrorDownloadAsync(RunningArchiveReadModel archive)
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

    private async Task CancelRemoteDownloadAsync(RemoteSourceDownloadReadModel download)
    {
        var result = await dialogService.ConfirmAsync(
            L["CancelRemoteDownloadTitle"],
            L["CancelRemoteDownloadConfirmation", download.FolderName],
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

        try
        {
            await operationRunner.RunAsync(
                (RemoteSourceDownloadStateService service) =>
                    service.CancelDownloadAsync(download.Id)
            );
            toastService.Success(L["RemoteDownloadCanceled", download.FolderName]);
        }
        catch (InvalidOperationException)
        {
            toastService.Error(L["RemoteDownloadStateChanged", download.FolderName]);
        }

        await OnRemoteDownloadCanceled.InvokeAsync();
    }
}
