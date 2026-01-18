using Bearcat.Frontend.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class CreateReleasePage(
    IDialogService dialogService,
    IConfiguration configuration) : OwningComponentBase
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
