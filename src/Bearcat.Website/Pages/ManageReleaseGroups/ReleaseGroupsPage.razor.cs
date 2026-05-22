using Bearcat.Domain.UseCases.ManageReleaseGroups;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseGroups;

public partial class ReleaseGroupsPage(
    IReleaseGroupReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService
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
            releaseGroups = await readRepository.GetAllAsync();
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
                IsEdit = true,
                ReleaseGroupId = releaseGroup.ReleaseGroupId,
            },
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

        var service = ScopedServices.GetRequiredService<ReleaseGroupService>();
        await service.DeleteAsync(releaseGroup.ReleaseGroupId);
        await LoadReleaseGroupsAsync();
    }
}
