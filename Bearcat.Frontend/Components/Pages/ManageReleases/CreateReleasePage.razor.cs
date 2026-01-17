using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class CreateReleasePage(IDialogService dialogService) : OwningComponentBase
{
    private CreateReleaseModel model = null!;
    
    private EditContext editContext = null!;
    
    protected override void OnInitialized()
    {
        model = new CreateReleaseModel();
        editContext = new EditContext(model);
    }
    
    private async Task ShowSelectFolderDialogAsync()
    {
        var dialog = await dialogService.ShowDialogAsync<FolderSelectionDialog>(new DialogParameters
            {
                Title = "Select release folder",
                Modal = true,
                PreventDismissOnOverlayClick = true,
            });

        var result = await dialog.Result;
        if (result is { Cancelled: false, Data: string selectedFolderPath })
        {
            model.FolderPath = selectedFolderPath;
        }
    }
}

