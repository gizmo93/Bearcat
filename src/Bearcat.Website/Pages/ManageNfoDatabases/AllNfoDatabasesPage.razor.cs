using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.UseCases.ManageNfoDatabases;
using Bearcat.Domain.UseCases.ManageNfoDatabases.Dto;
using Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageNfoDatabases;

public partial class AllNfoDatabasesPage(
    INfoDatabaseRegistrationReadRepository readRepository,
    INfoDatabaseFactory nfoDatabaseFactory,
    DialogService dialogService,
    ToastService toastService
)
{
    private IReadOnlyList<NfoDatabaseRegistrationDto> registrations = [];
    private NfoDatabaseRegistrationService service = null!;
    private bool CanAddRegistration =>
        registrations.Count < nfoDatabaseFactory.GetNfoDatabases().Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistrationsAsync();
        service = ScopedServices.GetRequiredService<NfoDatabaseRegistrationService>();
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

    private async Task ShowEditDialogAsync(NfoDatabaseRegistrationDto registration)
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

    private async Task ToggleIsActiveAsync(NfoDatabaseRegistrationDto registration)
    {
        await service.ToggleIsActiveAsync(registration.Id);

        toastService.Success(
            registration.IsActive
                ? L["NfoDatabaseRegistrationDeactivated", registration.NfoDatabaseName]
                : L["NfoDatabaseRegistrationActivated", registration.NfoDatabaseName]
        );
        await LoadRegistrationsAsync();
    }

    private async Task DeleteAsync(NfoDatabaseRegistrationDto registration)
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

        await service.DeleteAsync(registration.Id);
        await LoadRegistrationsAsync();
    }
}
