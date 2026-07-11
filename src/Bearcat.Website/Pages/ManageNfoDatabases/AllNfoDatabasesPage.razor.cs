using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.UseCases.ManageNfoDatabases;
using Bearcat.Domain.UseCases.ManageNfoDatabases.ReadModels;
using Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageNfoDatabases;

public partial class AllNfoDatabasesPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<NfoDatabaseRegistrationReadModel> registrations = [];
    private int availableDatabaseCount;
    private bool CanAddRegistration => registrations.Count < availableDatabaseCount;

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistrationsAsync();
        availableDatabaseCount = operationRunner.Run(
            (INfoDatabaseFactory factory) => factory.GetNfoDatabases().Count
        );
    }

    private async Task LoadRegistrationsAsync()
    {
        registrations = await operationRunner.RunAsync(
            (INfoDatabaseRegistrationReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            new DialogOpenOptions
            {
                Title = L["AddNfoDatabase"],
                Description = L["NfoDatabaseDialogDescription"],
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

    private async Task ShowEditDialogAsync(NfoDatabaseRegistrationReadModel registration)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditDialog.NfoDatabaseRegistrationId)] = registration.Id,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", registration.NfoDatabaseName],
                Description = L["NfoDatabaseDialogDescription"],
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

    private async Task ToggleIsActiveAsync(NfoDatabaseRegistrationReadModel registration)
    {
        await operationRunner.RunAsync(
            (NfoDatabaseRegistrationService service) => service.ToggleIsActiveAsync(registration.Id)
        );

        toastService.Success(
            registration.IsActive
                ? L["NfoDatabaseRegistrationDeactivated", registration.NfoDatabaseName]
                : L["NfoDatabaseRegistrationActivated", registration.NfoDatabaseName]
        );
        await LoadRegistrationsAsync();
    }

    private async Task DeleteAsync(NfoDatabaseRegistrationReadModel registration)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", registration.NfoDatabaseName],
            L["DeleteNfoDatabaseRegistrationConfirmation", registration.NfoDatabaseName],
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
            (NfoDatabaseRegistrationService service) => service.DeleteAsync(registration.Id)
        );
        await LoadRegistrationsAsync();
    }
}
