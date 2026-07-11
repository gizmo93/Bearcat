using Bearcat.Domain.UseCases.ManageQualityProfiles.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseGroups;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageReleaseGroups;

public partial class ReleaseGroupsPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadReleaseGroupsAsync();
    }

    private async Task LoadReleaseGroupsAsync()
    {
        isLoading = true;

        try
        {
            releaseGroups = await operationRunner.RunAsync(
                (IReleaseGroupReadRepository repository) => repository.GetAllAsync()
            );
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseGroupDialog.FormModel)] = new ReleaseGroupFormModel(),
            [nameof(CreateOrEditReleaseGroupDialog.QualityProfiles)] =
                await operationRunner.RunAsync(
                    (IQualityProfileReadRepository repository) => repository.GetAllAsync()
                ),
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseGroupDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["NewReleaseGroup"],
                Description = L["ReleaseGroupDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseGroupsAsync();
        }
    }

    private async Task ShowEditDialogAsync(ReleaseGroupReadModel releaseGroup)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseGroupDialog.FormModel)] = new ReleaseGroupFormModel
            {
                Name = releaseGroup.Name,
                EnableAutomaticReuploads = releaseGroup.EnableAutomaticReuploads,
                NumberOfHoursUntilReupload = releaseGroup.NumberOfHoursUntilReupload,
                QualityProfileId = releaseGroup.QualityProfileId,
                IsEdit = true,
                ReleaseGroupId = releaseGroup.ReleaseGroupId,
            },
            [nameof(CreateOrEditReleaseGroupDialog.QualityProfiles)] =
                await operationRunner.RunAsync(
                    (IQualityProfileReadRepository repository) => repository.GetAllAsync()
                ),
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseGroupDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", releaseGroup.Name],
                Description = L["ReleaseGroupDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseGroupsAsync();
        }
    }

    private async Task DeleteAsync(ReleaseGroupReadModel releaseGroup)
    {
        if (releaseGroup.AssignedReleaseCount > 0)
        {
            toastService.Error(L["ReleaseGroupDeleteBlocked", releaseGroup.Name]);
            return;
        }

        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", releaseGroup.Name],
            L["DeleteReleaseGroupConfirmation", releaseGroup.Name],
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

        await operationRunner.RunAsync(
            (ReleaseGroupService service) => service.DeleteAsync(releaseGroup.ReleaseGroupId)
        );
        await LoadReleaseGroupsAsync();
    }
}
