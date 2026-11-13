using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageUploads.OnlineChecks;
using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseBulkActionBar(
    IScopedOperationRunner operationRunner,
    ToastService toastService,
    DialogService dialogService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public SearchResultSelection Selection { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<int> VisibleReleaseIds { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ReleaseGroupReadModel> ReleaseGroups { get; set; } = [];

    [Parameter]
    public EventCallback OnSelectionChanged { get; set; }

    [Parameter]
    public EventCallback OnReleasesUpdated { get; set; }

    private IReadOnlyList<SelectOption<int>> ReleaseGroupOptions =>
        ReleaseGroups
            .Select(group => new SelectOption<int>(group.ReleaseGroupId, group.Name))
            .ToList();

    private IReadOnlyList<SelectOption<string>> PrimaryLanguageOptions =>
        PrimaryLanguageSelectOptions.Create(L["NotSet"]);

    private async Task SelectAllOnPageAsync()
    {
        Selection.SelectAll(VisibleReleaseIds);
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task ClearSelectionAsync()
    {
        Selection.Clear();
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task ApplyReleaseGroupAsync(int releaseGroupId)
    {
        var releaseIds = Selection.SelectedIds;

        await operationRunner.RunAsync(
            (ReleaseService service) => service.UpdateReleaseGroupAsync(releaseIds, releaseGroupId)
        );

        toastService.Success(L["ReleaseGroupChangedForReleases", releaseIds.Count]);
        Selection.Clear();
        await OnReleasesUpdated.InvokeAsync();
    }

    private async Task ApplyPrimaryLanguageAsync(string primaryLanguageCode)
    {
        var releaseIds = Selection.SelectedIds;

        await operationRunner.RunAsync(
            (ReleaseService service) =>
                service.UpdatePrimaryLanguageAsync(releaseIds, primaryLanguageCode)
        );

        toastService.Success(L["PrimaryLanguageChangedForReleases", releaseIds.Count]);
        Selection.Clear();
        await OnReleasesUpdated.InvokeAsync();
    }

    private async Task CreateManualReuploadsAsync()
    {
        var releaseIds = Selection.SelectedIds;

        var result = await dialogService.ConfirmAsync(
            L["ReuploadNotFullyOnlineHosters"],
            L["ReuploadNotFullyOnlineHostersOfReleasesConfirmation", releaseIds.Count],
            new ConfirmDialogOptions { ConfirmText = L["Reupload"], CancelText = L["Cancel"] }
        );

        if (!result.Confirmed)
        {
            return;
        }

        var createdReuploadCount = await operationRunner.RunAsync(
            (UploadCreationService service) =>
                service.CreateManualReuploadsForReleasesAsync(releaseIds)
        );

        if (createdReuploadCount > 0)
        {
            toastService.Success(L["ManualReuploadsCreated", createdReuploadCount]);
        }
        else
        {
            toastService.Info(L["NoHosterNeedsReupload"]);
        }
        Selection.Clear();
        await OnReleasesUpdated.InvokeAsync();
    }

    private async Task DeleteSelectedReleasesAsync()
    {
        var releaseIds = Selection.SelectedIds;

        var result = await dialogService.ConfirmAsync(
            L["DeleteSelectedReleases"],
            L["DeleteSelectedReleasesConfirmation", releaseIds.Count],
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

        await operationRunner.RunAsync((ReleaseService service) => service.DeleteAsync(releaseIds));

        toastService.Success(L["ReleasesDeleted", releaseIds.Count]);
        Selection.Clear();
        await OnReleasesUpdated.InvokeAsync();
    }

    private async Task StartOnlineStateCheckAsync()
    {
        var releaseIds = Selection.SelectedIds;
        _ = CheckOnlineStateInBackgroundAsync(releaseIds);

        toastService.Info(L["OnlineStateCheckStartedForReleases", releaseIds.Count]);
        Selection.Clear();
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task CheckOnlineStateInBackgroundAsync(IReadOnlyList<int> releaseIds)
    {
        try
        {
            var checkedUploadCount = await operationRunner.RunAsync(
                (UploadOnlineCheckService service) =>
                    service.CheckOnlineStateOfUploadsOfReleasesNowAsync(releaseIds)
            );

            if (checkedUploadCount > 0)
            {
                toastService.Success(L["OnlineStateCheckedForUploads", checkedUploadCount]);
            }
            else
            {
                toastService.Info(L["NoUploadsToCheckOnlineState"]);
            }

            await OnReleasesUpdated.InvokeAsync();
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception);
        }
    }
}
