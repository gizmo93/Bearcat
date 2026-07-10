using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.UseCases.ManageMediaDatabases;
using Bearcat.Domain.UseCases.ManageMediaDatabases.ReadModels;
using Bearcat.Domain.UseCases.ManageMediaDatabases.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageMediaDatabases;

public partial class AllMediaDatabasesPage(
    IMediaDatabaseRegistrationReadRepository readRepository,
    IMediaMetadataDatabaseFactory metadataDatabaseFactory,
    DialogService dialogService,
    ToastService toastService
)
{
    private IReadOnlyList<MediaDatabaseRegistrationReadModel> registrations = [];
    private MediaDatabaseRegistrationService service = null!;
    private bool CanAddRegistration =>
        registrations.Count < metadataDatabaseFactory.GetDatabases().Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistrationsAsync();
        service = ScopedServices.GetRequiredService<MediaDatabaseRegistrationService>();
    }

    private async Task LoadRegistrationsAsync()
    {
        registrations = await readRepository.GetAllAsync();
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
        var result = await service.TryLoginAsync(registration.Id);

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
        await service.ToggleIsActiveAsync(registration.Id);

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

        await service.DeleteAsync(registration.Id);
        await LoadRegistrationsAsync();
    }
}
