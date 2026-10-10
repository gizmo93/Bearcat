using Bearcat.Domain.UseCases.ManageArchiveStorageFolders;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.ReadModels;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageArchiveStorageFolders;

public partial class ArchiveStorageFoldersPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<ArchiveStorageFolderWithAvailableFreeSpaceReadModel> storageFolders = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadStorageFoldersAsync();
    }

    private async Task LoadStorageFoldersAsync()
    {
        storageFolders = await operationRunner.RunAsync(
            (ArchiveStorageFolderService service) => service.GetAllWithAvailableFreeSpaceAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        await ShowDialogAsync(new ArchiveStorageFolderFormModel(), L["NewArchiveStorageFolder"]);
    }

    private async Task ShowEditDialogAsync(ArchiveStorageFolderReadModel storageFolder)
    {
        var formModel = new ArchiveStorageFolderFormModel
        {
            ArchiveStorageFolderId = storageFolder.Id,
            Name = storageFolder.Name,
            Path = storageFolder.Path,
            MinimumFreeSpaceGb = storageFolder.MinimumFreeSpaceGb,
            Priority = storageFolder.Priority,
            RetrieveArchivesBeforeReupload = storageFolder.RetrieveArchivesBeforeReupload,
        };

        await ShowDialogAsync(formModel, L["EditNamedItem", storageFolder.Name]);
    }

    private async Task ShowDialogAsync(ArchiveStorageFolderFormModel formModel, string title)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditArchiveStorageFolderDialog.FormModel)] = formModel,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditArchiveStorageFolderDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = title,
                Description = L["ArchiveStorageFolderDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadStorageFoldersAsync();
        }
    }

    private async Task ToggleIsActiveAsync(ArchiveStorageFolderReadModel storageFolder)
    {
        await operationRunner.RunAsync(
            (ArchiveStorageFolderService service) => service.ToggleIsActiveAsync(storageFolder.Id)
        );

        toastService.Success(
            storageFolder.IsActive
                ? L["ArchiveStorageFolderDeactivated", storageFolder.Name]
                : L["ArchiveStorageFolderActivated", storageFolder.Name]
        );
        await LoadStorageFoldersAsync();
    }

    private async Task DeleteAsync(ArchiveStorageFolderReadModel storageFolder)
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", storageFolder.Name],
            L["DeleteArchiveStorageFolderConfirmation", storageFolder.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        var result = await operationRunner.RunAsync(
            (ArchiveStorageFolderService service) => service.DeleteAsync(storageFolder.Id)
        );

        if (!result.IsDeleted)
        {
            toastService.Error(
                L[
                    "ArchiveStorageFolderStillStoresArchives",
                    storageFolder.Name,
                    result.StoredArchiveCount
                ]
            );
            return;
        }

        toastService.Success(L["ArchiveStorageFolderDeleted", storageFolder.Name]);
        await LoadStorageFoldersAsync();
    }
}
