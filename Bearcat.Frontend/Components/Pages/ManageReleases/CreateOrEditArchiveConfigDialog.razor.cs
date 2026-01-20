using BearCat.Core.Domain.Abstractions.Archiver;
using Bearcat.Frontend.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class CreateOrEditArchiveConfigDialog(
    IArchiverFactory archiverFactory,
    IConfiguration configuration,
    IDialogService dialogService)
{
    [Parameter]
    public ArchiveConfigFormModel FormModel { get; set; } = null!;
    
    [Parameter]
    public int ReleaseId { get; set; }
    
    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;

    private IReadOnlyList<ArchiverDto> archivers = [];
    
    private ArchiverDto? SelectedArchiver => archivers.FirstOrDefault(
        a => a.ClassName == FormModel.ArchiverName);
    
    private EditContext editContext = null!;
    
    private ValidationMessageStore messageStore = null!;
    
    protected override void OnInitialized()
    {
        archivers = archiverFactory.GetArchivers();
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
    }

    private Task SaveAsync()
    {
        MudDialog.Close();
        return Task.CompletedTask;
    }

    private async Task ShowSelectFolderDialogAsync()
    {
        var parameters = new DialogParameters<FolderSelectionDialog>
        {
            { dlg => dlg.BaseFolderPath, configuration.GetRequiredSection("ReleaseDataDirectory").Value! }
        };
        
        var dialog = await dialogService.ShowAsync<FolderSelectionDialog>(
            "Select a folder, where the archive files should be created",
            parameters,
            new DialogOptions
            {
                BackdropClick = false,
                CloseOnEscapeKey = true,
                CloseButton = true,
                FullWidth = true,
            });
        
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: string selectedPath })
        {
            FormModel.ArchiveFilesBasePath = selectedPath;
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (FormModel.ArchiverName is null)
        {
            messageStore.Add(() => FormModel.ArchiverName!, "You must select an archiver");
        }
        
        if (string.IsNullOrWhiteSpace(FormModel.ArchiveFilesBasePath))
        {
            messageStore.Add(() => FormModel.ArchiveFilesBasePath!, "Base path is required");
        }
        
        if (string.IsNullOrWhiteSpace(FormModel.ArchiveNamePrefix))
        {
            messageStore.Add(() => FormModel.ArchiveNamePrefix!, "Archive name prefix is required");
        }

        if (FormModel.ArchiveFileSizeMb < 0)
        {
            messageStore.Add(() => FormModel.ArchiveFileSizeMb, "Archive file size must be zero or greater");
        }
    }
}
