using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningRemoteDownloads;

public partial class RunningRemoteDownloads(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RemoteSourceDownloadReadModel> Downloads { get; set; } = null!;

    [Parameter]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> DownloadProgress { get; set; } =
        new Dictionary<int, TransferProgressSnapshot>();

    [Parameter]
    public EventCallback OnDownloadCanceled { get; set; }

    private readonly HashSet<int> showDetailIds = [];

    private IReadOnlyList<RemoteSourceDownloadReadModel> ExpandedDownloads =>
        Downloads
            .Where(download =>
                showDetailIds.Contains(download.Id) && DownloadProgress.ContainsKey(download.Id)
            )
            .ToList();

    private double GetProgress(RemoteSourceDownloadReadModel download)
    {
        return DownloadProgress.TryGetValue(download.Id, out var snapshot)
            ? snapshot.Percentage
            : 0;
    }

    private void ToggleShowDetails(int downloadId)
    {
        if (!showDetailIds.Remove(downloadId))
        {
            showDetailIds.Add(downloadId);
        }
    }

    private async Task CancelDownloadAsync(RemoteSourceDownloadReadModel download)
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

        await OnDownloadCanceled.InvokeAsync();
    }
}
