using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.UseCases.ManageMediaDatabases;
using Bearcat.Domain.UseCases.ManageMediaDatabases.ReadModels;
using Bearcat.Domain.UseCases.ManageMediaDatabases.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageMediaDatabases;

public partial class AllMediaDatabasesPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<MediaDatabaseRegistrationReadModel> registrations = [];
    private int availableDatabaseCount;
    private bool CanAddRegistration => registrations.Count < availableDatabaseCount;

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistrationsAsync();
        availableDatabaseCount = operationRunner.Run(
            (IMediaMetadataDatabaseFactory factory) => factory.GetDatabases().Count
        );
    }

    private async Task LoadRegistrationsAsync()
    {
        registrations = await operationRunner.RunAsync(
            (IMediaDatabaseRegistrationReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            new DialogOpenOptions
            {
                Title = L["AddMediaDatabase"],
                Description = L["MediaDatabaseDialogDescription"],
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

    private async Task ShowEditDialogAsync(MediaDatabaseRegistrationReadModel registration)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditDialog.MediaDatabaseRegistrationId)] = registration.Id,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", registration.MediaDatabaseName],
                Description = L["MediaDatabaseDialogDescription"],
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

    private async Task TryLoginAsync(MediaDatabaseRegistrationReadModel registration)
    {
        var result = await operationRunner.RunAsync(
            (MediaDatabaseRegistrationService service) => service.TryLoginAsync(registration.Id)
        );

        if (result.IsSuccess)
        {
            toastService.Success(L["LoginSuccessful", registration.MediaDatabaseName]);
            return;
        }

        toastService.Error(
            L["LoginFailed", registration.MediaDatabaseName, result.ErrorMessage ?? string.Empty]
        );
    }

    private async Task ToggleIsActiveAsync(MediaDatabaseRegistrationReadModel registration)
    {
        await operationRunner.RunAsync(
            (MediaDatabaseRegistrationService service) =>
                service.ToggleIsActiveAsync(registration.Id)
        );

        toastService.Success(
            registration.IsActive
                ? L["MediaDatabaseRegistrationDeactivated", registration.MediaDatabaseName]
                : L["MediaDatabaseRegistrationActivated", registration.MediaDatabaseName]
        );
        await LoadRegistrationsAsync();
    }

    private async Task DeleteAsync(MediaDatabaseRegistrationReadModel registration)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", registration.MediaDatabaseName],
            L["DeleteMediaDatabaseRegistrationConfirmation", registration.MediaDatabaseName],
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
            (MediaDatabaseRegistrationService service) => service.DeleteAsync(registration.Id)
        );
        await LoadRegistrationsAsync();
    }
}
