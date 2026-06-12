using Bearcat.Abstractions.SeriesDatabase;
using Bearcat.Domain.UseCases.ManageSeriesDatabases;
using Bearcat.Domain.UseCases.ManageSeriesDatabases.ReadModels;
using Bearcat.Domain.UseCases.ManageSeriesDatabases.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageSeriesDatabases;

public partial class AllSeriesDatabasesPage(
    ISeriesDatabaseRegistrationReadRepository readRepository,
    ISeriesDatabaseFactory seriesDatabaseFactory,
    DialogService dialogService,
    ToastService toastService
)
{
    private IReadOnlyList<SeriesDatabaseRegistrationReadModel> registrations = [];
    private SeriesDatabaseRegistrationService service = null!;
    private bool CanAddRegistration =>
        registrations.Count < seriesDatabaseFactory.GetSeriesDatabases().Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistrationsAsync();
        service = ScopedServices.GetRequiredService<SeriesDatabaseRegistrationService>();
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
                Title = L["AddSeriesDatabase"],
                Description = L["SeriesDatabaseDialogDescription"],
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

    private async Task ShowEditDialogAsync(SeriesDatabaseRegistrationReadModel registration)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditDialog.SeriesDatabaseRegistrationId)] = registration.Id,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", registration.SeriesDatabaseName],
                Description = L["SeriesDatabaseDialogDescription"],
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

    private async Task TryLoginAsync(SeriesDatabaseRegistrationReadModel registration)
    {
        var result = await service.TryLoginAsync(registration.Id);

        if (result.IsSuccess)
        {
            toastService.Success(L["LoginSuccessful", registration.SeriesDatabaseName]);
            return;
        }

        toastService.Error(
            L["LoginFailed", registration.SeriesDatabaseName, result.ErrorMessage ?? string.Empty]
        );
    }

    private async Task ToggleIsActiveAsync(SeriesDatabaseRegistrationReadModel registration)
    {
        await service.ToggleIsActiveAsync(registration.Id);

        toastService.Success(
            registration.IsActive
                ? L["SeriesDatabaseRegistrationDeactivated", registration.SeriesDatabaseName]
                : L["SeriesDatabaseRegistrationActivated", registration.SeriesDatabaseName]
        );
        await LoadRegistrationsAsync();
    }

    private async Task DeleteAsync(SeriesDatabaseRegistrationReadModel registration)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", registration.SeriesDatabaseName],
            L["DeleteSeriesDatabaseRegistrationConfirmation", registration.SeriesDatabaseName],
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
