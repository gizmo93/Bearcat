using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class CreateOrEditReleaseDialog(
    IDialogService dialogService,
    IConfiguration configuration,
    NavigationManager navigationManager
) : OwningComponentBase
{
    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;

    private ReleaseFormModel formModel = null!;

    private EditContext editContext = null!;

    private ValidationMessageStore? messageStore;

    protected override void OnInitialized()
    {
        formModel = new ReleaseFormModel();
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseService>();

        var releaseType = (ReleaseType)Convert.ToInt32(formModel.ReleaseType);

        var id = await service.CreateAsync(
            name: formModel.Name,
            releaseFolderPath: formModel.FolderPath,
            releaseType: releaseType
        );

        navigationManager.NavigateTo("releases");
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            messageStore.Add(() => formModel.Name, "Name is required");
        }

        if (string.IsNullOrWhiteSpace(formModel.FolderPath))
        {
            messageStore.Add(() => formModel.FolderPath, "You must select a folder");
        }

        if (formModel.ReleaseType is null)
        {
            messageStore.Add(() => formModel.ReleaseType!, "You must select a release type");
        }
    }

    private async Task ShowSelectFolderDialogAsync()
    {
        var releasesPath = configuration.GetRequiredSection("ReleaseDataDirectory").Value!;

        var parameters = new DialogParameters<FolderSelectionDialog>
        {
            { dlg => dlg.BaseFolderPath, releasesPath },
        };

        var dialog = await dialogService.ShowAsync<FolderSelectionDialog>(
            "Select release folder",
            parameters,
            new DialogOptions { CloseButton = true, FullWidth = true }
        );

        var result = await dialog.Result;

        if (result is { Canceled: false, Data: string folderPath })
        {
            formModel.FolderPath = folderPath;

            if (string.IsNullOrWhiteSpace(formModel.Name))
            {
                formModel.Name = Path.GetFileName(formModel.FolderPath);
            }
        }
    }
}
