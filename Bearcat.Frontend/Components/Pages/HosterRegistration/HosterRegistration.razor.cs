using BearCat.Core.Domain.UseCases.ManageHosters;
using BearCat.Core.Domain.UseCases.ManageHosters.Dto;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.HosterRegistration;

public partial class HosterRegistration(
    IHosterConfigurationReadRepository readRepository,
    HosterRegistrationService service,
    IDialogService dialogService)
    : ComponentBase
{
    private IQueryable<HosterRegistrationDto> hosters = Enumerable.Empty<HosterRegistrationDto>().AsQueryable();
    
    protected override async Task OnInitializedAsync()
    {
        await LoadHostersAsync();
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
        await service.ToggleIsActiveAsync(hoster.Id);
        await LoadHostersAsync();
    }

    private async Task DeleteAsync(HosterRegistrationDto hoster)
    {
        await service.RemoveAsync(hoster.Id);
        await LoadHostersAsync();
    }
}

