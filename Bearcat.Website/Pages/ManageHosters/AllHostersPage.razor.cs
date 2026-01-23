using BearCat.Core.Domain.UseCases.ManageHosters;
using BearCat.Core.Domain.UseCases.ManageHosters.Dto;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageHosters;

public partial class AllHostersPage(
    IHosterConfigurationReadRepository readRepository,
    IDialogService dialogService,
    ISnackbar snackbar)

{
    private IReadOnlyList<HosterRegistrationDto> hosters = [];
    private HosterRegistrationService hosterRegistrationService = null!;
    
    private MudMenu contextMenu = null!;

    private HosterRegistrationDto? contextMenuRow;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadHostersAsync();
        hosterRegistrationService = ScopedServices.GetRequiredService<HosterRegistrationService>();
    }

    private async Task LoadHostersAsync()
    {
        hosters = await readRepository.GetAllRegistrationsAsync();
    }
    
    private async Task OpenMenuContent(DataGridRowClickEventArgs<HosterRegistrationDto> args)
    {
        contextMenuRow = args.Item;
        await contextMenu.OpenMenuAsync(args.MouseEventArgs);
    }

    private async Task ShowAddDialogAsync()
    {
        var formModel = new HosterFormModel();
        
        var parameters = new DialogParameters<AddOrEditHoster> { { x => x.FormModel, formModel } };
        
        var dialog = await dialogService.ShowAsync<AddOrEditHoster>("Add Hoster", parameters, new DialogOptions
        {
            BackdropClick = false,
            FullWidth = true,
        });

        await dialog.Result;
        await LoadHostersAsync();
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

        var parameters = new DialogParameters<AddOrEditHoster> { { x => x.FormModel, formModel } };
        
        var dialog = await dialogService.ShowAsync<AddOrEditHoster>($"Edit {hosterRegistration.Name}", parameters, new DialogOptions
        {
            BackdropClick = false,
            FullWidth = true,
        });

        await dialog.Result;
        await LoadHostersAsync();
    }

    private async Task ToggleIsActiveAsync(HosterRegistrationDto hoster)
    {
        await hosterRegistrationService.ToggleIsActiveAsync(hoster.Id);
        
        var status = hoster.IsActive ? "deactivated" : "activated";
        snackbar.Add($"Hoster registration {hoster.Name} {status}", Severity.Success);
        await LoadHostersAsync();
    }

    private async Task DeleteAsync(HosterRegistrationDto hoster)
    {
        var message = $"Are you sure you want to delete hoster registration {hoster.Name}?" +
                      $"\nBe careful, as it will also remove all uploads related to that hoster registration.";
        
        var result = await dialogService.ShowMessageBoxAsync(title: $"Delete {hoster.Name}",
            message: message,
            yesText: "Delete",
            noText: "Cancel");

        if (result == true)
        {
            await hosterRegistrationService.RemoveAsync(hoster.Id);
        }
        
        await LoadHostersAsync();   
    }
    
    private async Task TryLoginAsync(HosterRegistrationDto hoster)
    {
        var result = await hosterRegistrationService.TryLoginAsync(hoster.Id);
        
        if (result.IsSuccess)
        {
            snackbar.Add($"Login for registration {hoster.Name} successful", Severity.Success);
        }
        else
        {
            snackbar.Add($"Login for registration {hoster.Name} failed: {result.ErrorMessage}", Severity.Error);
        }
    }
}

