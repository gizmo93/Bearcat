using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Blueprint.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint.Pages.ManageReleases;

public partial class CreateOrEditReleaseDialog(
    DialogService dialogService,
    IConfiguration configuration,
    NavigationManager navigationManager
) : OwningComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private ReleaseFormModel formModel = null!;
    private EditContext editContext = null!;
    private ValidationMessageStore? messageStore;
    private string? folderValidationMessage;

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

        await DialogRef.CloseAsync(DialogResult.Ok(id));
        navigationManager.NavigateTo("releases");
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        folderValidationMessage = null;
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            messageStore.Add(() => formModel.Name, "Name is required");
        }

        if (string.IsNullOrWhiteSpace(formModel.FolderPath))
        {
            folderValidationMessage = "You must select a folder";
            messageStore.Add(() => formModel.FolderPath, folderValidationMessage);
        }

        if (formModel.ReleaseType is null)
        {
            messageStore.Add(() => formModel.ReleaseType!, "You must select a release type");
        }
    }

    private async Task OpenFolderDialogAsync()
    {
        var releasesPath = configuration.GetRequiredSection("ReleaseDataDirectory").Value!;
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPath)] = releasesPath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = "Select release folder",
                Description = "Choose a folder below to use as the release root.",
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (result.Cancelled)
        {
            return;
        }

        var folderPath = result.GetData<string>();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        formModel.FolderPath = folderPath;
        folderValidationMessage = null;

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            formModel.Name = Path.GetFileName(folderPath);
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
