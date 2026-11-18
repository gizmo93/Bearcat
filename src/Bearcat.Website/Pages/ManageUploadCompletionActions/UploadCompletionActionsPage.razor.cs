using Bearcat.Domain.UseCases.ManageUploadCompletionActions;
using Bearcat.Domain.UseCases.ManageUploadCompletionActions.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadCompletionActions.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageUploadCompletionActions;

public partial class UploadCompletionActionsPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<UploadCompletionActionRegistrationReadModel> registrations = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistrationsAsync();
    }

    private async Task LoadRegistrationsAsync()
    {
        registrations = await operationRunner.RunAsync(
            (IUploadCompletionActionRegistrationReadRepository repository) =>
                repository.GetAllAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        await OpenDialogAsync(registration: null, L["AddUploadCompletionAction"]);
    }

    private async Task ShowEditDialogAsync(UploadCompletionActionRegistrationReadModel registration)
    {
        await OpenDialogAsync(registration, L["EditNamedItem", registration.Name]);
    }

    private async Task OpenDialogAsync(
        UploadCompletionActionRegistrationReadModel? registration,
        string title
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddOrEditUploadCompletionAction.Registration)] = registration,
        };

        var dialog = await dialogService.OpenAsync<AddOrEditUploadCompletionAction>(
            parameters,
            new DialogOpenOptions
            {
                Title = title,
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadRegistrationsAsync();
        }
    }

    private async Task ToggleIsActiveAsync(UploadCompletionActionRegistrationReadModel registration)
    {
        await operationRunner.RunAsync(
            (UploadCompletionActionRegistrationService service) =>
                service.ToggleIsActiveAsync(registration.Id)
        );

        toastService.Success(
            registration.IsActive
                ? L["UploadCompletionActionDeactivated", registration.Name]
                : L["UploadCompletionActionActivated", registration.Name]
        );

        await LoadRegistrationsAsync();
    }

    private async Task DeleteAsync(UploadCompletionActionRegistrationReadModel registration)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", registration.Name],
            L["DeleteUploadCompletionActionConfirmation", registration.Name],
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
            (UploadCompletionActionRegistrationService service) =>
                service.RemoveAsync(registration.Id)
        );

        toastService.Success(L["UploadCompletionActionDeleted", registration.Name]);
        await LoadRegistrationsAsync();
    }
}
