using BearCat.Core.Domain.UseCases.ManageReleases;
using BearCat.Core.Domain.ValueObjects;
using Bearcat.Frontend.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class CreateReleasePage(
    IDialogService dialogService,
    IConfiguration configuration,
    NavigationManager navigationManager) : OwningComponentBase
{
    private CreateReleaseModel model = null!;

    private EditContext editContext = null!;
    
    private ValidationMessageStore? messageStore;

    private bool isValid;

    protected override void OnInitialized()
    {
        model = new CreateReleaseModel();
        editContext = new EditContext(model);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseService>();

        var releaseType = (ReleaseType)Convert.ToInt32(model.ReleaseType);
        
        var id = await service.CreateAsync(
            name: model.Name,
            releaseFolderPath: model.FolderPath,
            releaseType: releaseType);
        
        navigationManager.NavigateTo("releases");
    }

    private void HandleValidationRequested(
        object? sender,
        ValidationRequestedEventArgs args)
    {
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            messageStore.Add(() => model.Name, "Name is required");
        }
        
        if (string.IsNullOrWhiteSpace(model.FolderPath))
        {
            messageStore.Add(() => model.FolderPath, "You must select a folder");
        }

        if (string.IsNullOrWhiteSpace(model.ReleaseType) || model.ReleaseType == "0")
        {
            messageStore.Add(() => model.ReleaseType, "You must select a release type");
        }
    }

    private async Task ShowSelectFolderDialogAsync()
    {
        var releasesPath = configuration.GetRequiredSection("ReleaseDataDirectory").Value!;
        var dialog = await dialogService.ShowDialogAsync<FolderSelectionDialog>(
            data: releasesPath,
            parameters: new DialogParameters
            {
                Title = "Select release folder", Modal = true, PreventDismissOnOverlayClick = true,
            });

        var result = await dialog.Result;
        if (result is { Cancelled: false, Data: string selectedFolderPath })
        {
            model.FolderPath = selectedFolderPath;
        }
    }
}
