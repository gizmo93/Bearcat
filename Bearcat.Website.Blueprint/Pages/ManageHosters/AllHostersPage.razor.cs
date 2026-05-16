using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Domain.UseCases.ManageHosters.Dto;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint.Pages.ManageHosters;

public partial class AllHostersPage(
    IHosterConfigurationReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService
)
{
    private IReadOnlyList<HosterRegistrationDto> hosters = [];
    private HosterRegistrationService hosterRegistrationService = null!;

    protected override async Task OnInitializedAsync()
    {
        await LoadHostersAsync();
        hosterRegistrationService = ScopedServices.GetRequiredService<HosterRegistrationService>();
    }

    private async Task LoadHostersAsync()
    {
        hosters = await readRepository.GetAllRegistrationsAsync();
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddOrEditHoster.FormModel)] = new HosterFormModel(),
        };

        var dialog = await dialogService.OpenAsync<AddOrEditHoster>(
            parameters,
            new DialogOpenOptions
            {
                Title = "Add hoster",
                Description = "Save hoster credentials and configuration for uploads.",
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadHostersAsync();
        }
    }

    private async Task ShowEditDialogAsync(HosterRegistrationDto hosterRegistration)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddOrEditHoster.FormModel)] = new HosterFormModel
            {
                Name = hosterRegistration.Name,
                Configuration = hosterRegistration.Configuration.ToDictionary(),
                FullClassName = hosterRegistration.FullClassName,
                IsEdit = true,
                HosterRegistrationId = hosterRegistration.Id,
            },
        };

        var dialog = await dialogService.OpenAsync<AddOrEditHoster>(
            parameters,
            new DialogOpenOptions
            {
                Title = $"Edit {hosterRegistration.Name}",
                Description = "Save hoster credentials and configuration for uploads.",
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadHostersAsync();
        }
    }

    private async Task ToggleIsActiveAsync(HosterRegistrationDto hoster)
    {
        await hosterRegistrationService.ToggleIsActiveAsync(hoster.Id);

        var status = hoster.IsActive ? "deactivated" : "activated";
        toastService.Success($"Hoster registration {hoster.Name} {status}");
        await LoadHostersAsync();
    }

    private async Task DeleteAsync(HosterRegistrationDto hoster)
    {
        var result = await dialogService.ConfirmAsync(
            $"Delete {hoster.Name}",
            $"Are you sure you want to delete hoster registration {hoster.Name}? This also removes uploads tied to that registration.",
            new ConfirmDialogOptions
            {
                ConfirmText = "Delete",
                CancelText = "Cancel",
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await hosterRegistrationService.RemoveAsync(hoster.Id);
        await LoadHostersAsync();
    }

    private async Task TryLoginAsync(HosterRegistrationDto hoster)
    {
        await using var scope = ScopedServices.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<HosterRegistrationService>();

        var result = await service.TryLoginAsync(hoster.Id);

        if (result.IsSuccess)
        {
            toastService.Success($"Login for registration {hoster.Name} successful");
            return;
        }

        toastService.Error($"Login for registration {hoster.Name} failed: {result.ErrorMessage}");
    }
}
