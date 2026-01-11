using BearCat.Core.Domain.UseCases.ManageHosters;
using BearCat.Core.Domain.UseCases.ManageHosters.Dto;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.HosterRegistration;

public partial class HosterRegistration(
    IHosterConfigurationReadRepository readRepository,
    IDialogService dialogService,
    IToastService toastService)

{
    private IQueryable<HosterRegistrationDto> hosters = Enumerable.Empty<HosterRegistrationDto>().AsQueryable();
    private HosterRegistrationService hosterRegistrationService = null!;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadHostersAsync();
        hosterRegistrationService = ScopedServices.GetRequiredService<HosterRegistrationService>();
    }

    private async Task LoadHostersAsync()
    {
        hosters = (await readRepository.GetAllRegistrationsAsync()).AsQueryable();
    }

    private async Task ShowAddDialogAsync()
    {
        var formModel = new HosterFormModel();
        var dialog = await dialogService.ShowDialogAsync<AddOrEditHoster>(formModel,
            new DialogParameters
            {
                Title = "Add hoster",
                Modal = true,
                PreventDismissOnOverlayClick = true,
            });

        await dialog.Result;
        await LoadHostersAsync();
    }
    
    private async Task ShowEditDialogAsync(HosterRegistrationDto hosterRegistration)
    {
        var formModel = new HosterFormModel
        {
            Name = hosterRegistration.Name,
            Configuration = hosterRegistration.Configuration.ToDictionary(),
            FullClassName = hosterRegistration.FullClassName,
            IsEdit = true,
            HosterRegistrationId = hosterRegistration.Id,
        };
        
        var dialog = await dialogService.ShowDialogAsync<AddOrEditHoster>(formModel,
            new DialogParameters
            {
                Title = $"Edit {hosterRegistration.Name}",
                Modal = true,
                PreventDismissOnOverlayClick = true,
            });

        await dialog.Result;
        await LoadHostersAsync();
    }

    private async Task ToggleIsActiveAsync(HosterRegistrationDto hoster)
    {
        await hosterRegistrationService.ToggleIsActiveAsync(hoster.Id);
        await LoadHostersAsync();
    }

    private async Task DeleteAsync(HosterRegistrationDto hoster)
    {
        await hosterRegistrationService.RemoveAsync(hoster.Id);
        await LoadHostersAsync();
    }
    
    private async Task TryLoginAsync(HosterRegistrationDto hoster)
    {
        var result = await hosterRegistrationService.TryLoginAsync(hoster.Id);
        
        const int timeoutMilliseconds = 10_000;

        if (result.IsSuccess)
        {
            toastService.ShowSuccess(
                $"Login for registration {hoster.Name} successful", timeout: timeoutMilliseconds);
        }
        else
        {
            toastService.ShowError(
                $"Login for registration {hoster.Name} failed: {result.ErrorMessage}", timeout: timeoutMilliseconds);
        }
    }
}

