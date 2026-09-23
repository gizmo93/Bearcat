using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Dto;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageRemoteSourceDownloads;

public partial class RemoteSourceDownloadsPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    private const int PageSize = 25;

    private IReadOnlyList<RemoteSourceDownloadReadModel> downloads = [];
    private int totalCount;
    private int pageIndex;
    private bool isLoading;
    private RemoteSourceDownloadState? selectedState;

    private IReadOnlyList<SelectOption<RemoteSourceDownloadState?>> StateOptions =>
        [
            new(null, L["AllStatesExceptIgnored"]),
            .. Enum.GetValues<RemoteSourceDownloadState>()
                .Select(state => new SelectOption<RemoteSourceDownloadState?>(
                    state,
                    L.Localize(state)
                )),
        ];

    private int CurrentPage => pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * PageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * PageSize);

    protected override async Task OnInitializedAsync()
    {
        await LoadDownloadsAsync();
    }

    private async Task LoadDownloadsAsync()
    {
        isLoading = true;

        try
        {
            var result = await operationRunner.RunAsync(
                (IRemoteSourceDownloadReadRepository repository) =>
                    repository.SearchAsync(
                        new RemoteSourceDownloadSearchQuery(
                            States: GetFilteredStates(),
                            PageIndex: pageIndex,
                            PageSize: PageSize
                        )
                    )
            );

            downloads = result.Items;
            totalCount = result.TotalCount;

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await LoadDownloadsAsync();
            }
        }
        finally
        {
            isLoading = false;
        }
    }

    private IReadOnlyList<RemoteSourceDownloadState> GetFilteredStates()
    {
        return selectedState is { } state
            ? [state]
            : Enum.GetValues<RemoteSourceDownloadState>()
                .Where(value => value is not RemoteSourceDownloadState.Ignored)
                .ToList();
    }

    private async Task OnStateChangedAsync()
    {
        pageIndex = 0;
        await LoadDownloadsAsync();
    }

    private async Task GoToPageAsync(int page)
    {
        pageIndex = page - 1;
        await LoadDownloadsAsync();
    }

    private static bool HasActions(RemoteSourceDownloadReadModel download)
    {
        return download.CanCancel
            || download.CanRestart
            || download.CanRetryReleaseCreation
            || download.CanIgnore;
    }

    private async Task CancelAsync(RemoteSourceDownloadReadModel download)
    {
        if (
            !await ConfirmAsync(
                L["CancelRemoteDownloadTitle"],
                L["CancelRemoteDownloadConfirmation", download.FolderName],
                L["CancelDownload"]
            )
        )
        {
            return;
        }

        await ChangeStateAsync(
            download,
            (service, id) => service.CancelDownloadAsync(id),
            L["RemoteDownloadCanceled", download.FolderName]
        );
    }

    private async Task RestartAsync(RemoteSourceDownloadReadModel download)
    {
        if (
            !await ConfirmAsync(
                L["RestartDownload"],
                L[
                    "RestartRemoteDownloadConfirmation",
                    download.FolderName,
                    download.LocalFolderPath
                ],
                L["RestartDownload"]
            )
        )
        {
            return;
        }

        await ChangeStateAsync(
            download,
            (service, id) => service.RestartDownloadAsync(id),
            L["RemoteDownloadRestarted", download.FolderName]
        );
    }

    private async Task RetryReleaseCreationAsync(RemoteSourceDownloadReadModel download)
    {
        await ChangeStateAsync(
            download,
            (service, id) => service.RetryReleaseCreationAsync(id),
            L["ReleaseCreationQueuedAgain", download.FolderName]
        );
    }

    private async Task IgnoreAsync(RemoteSourceDownloadReadModel download)
    {
        await ChangeStateAsync(
            download,
            (service, id) => service.IgnoreDownloadAsync(id),
            L["RemoteDownloadIgnored", download.FolderName]
        );
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var result = await dialogService.ConfirmAsync(
            title,
            message,
            new ConfirmDialogOptions
            {
                ConfirmText = confirmText,
                CancelText = L["Close"],
                Destructive = true,
            }
        );

        return result.Confirmed;
    }

    private async Task ChangeStateAsync(
        RemoteSourceDownloadReadModel download,
        Func<RemoteSourceDownloadStateService, int, Task> stateChange,
        string successMessage
    )
    {
        try
        {
            await operationRunner.RunAsync(
                (RemoteSourceDownloadStateService service) => stateChange(service, download.Id)
            );
            toastService.Success(successMessage);
        }
        catch (InvalidOperationException)
        {
            toastService.Error(L["RemoteDownloadStateChanged", download.FolderName]);
        }

        await LoadDownloadsAsync();
    }
}
