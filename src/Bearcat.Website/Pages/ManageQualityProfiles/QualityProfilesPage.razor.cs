using Bearcat.Domain.UseCases.ManageQualityProfiles;
using Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;
using Bearcat.Domain.UseCases.ManageQualityProfiles.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageQualityProfiles;

public partial class QualityProfilesPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<QualityProfileReadModel> profiles = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadProfilesAsync();
    }

    private async Task LoadProfilesAsync()
    {
        isLoading = true;

        try
        {
            profiles = await operationRunner.RunAsync(
                (IQualityProfileReadRepository repository) => repository.GetAllAsync()
            );
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ShowAddDialogAsync()
    {
        await ShowDialogAsync(new QualityProfileFormModel(), L["NewQualityProfile"]);
    }

    private async Task ShowEditDialogAsync(QualityProfileReadModel profile)
    {
        var detail = await operationRunner.RunAsync(
            (IQualityProfileReadRepository repository) => repository.GetDetailAsync(profile.Id)
        );

        if (detail is null)
        {
            return;
        }

        var formModel = new QualityProfileFormModel
        {
            Name = detail.Name,
            IsEdit = true,
            QualityProfileId = detail.Id,
            Rules = detail.Rules,
        };

        await ShowDialogAsync(formModel, L["EditNamedItem", profile.Name]);
    }

    private async Task ShowDialogAsync(QualityProfileFormModel formModel, string title)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditQualityProfileDialog.FormModel)] = formModel,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditQualityProfileDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = title,
                Description = L["QualityProfileDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadProfilesAsync();
        }
    }

    private async Task DeleteAsync(QualityProfileReadModel profile)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", profile.Name],
            L["DeleteQualityProfileConfirmation", profile.Name],
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
            (QualityProfileService service) => service.DeleteAsync(profile.Id)
        );

        toastService.Success(L["QualityProfileDeleted", profile.Name]);
        await LoadProfilesAsync();
    }
}
