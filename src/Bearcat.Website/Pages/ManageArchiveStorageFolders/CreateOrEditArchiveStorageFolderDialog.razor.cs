using Bearcat.Domain.UseCases.ManageArchiveStorageFolders;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Validation;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageArchiveStorageFolders;

public partial class CreateOrEditArchiveStorageFolderDialog(
    DialogService dialogService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public ArchiveStorageFolderFormModel FormModel { get; set; } = new();

    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isSaving;

    private static IReadOnlyList<string> FileSystemRootPaths =>
        OperatingSystem.IsWindows() ? Directory.GetLogicalDrives() : ["/"];

    protected override void OnInitialized()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += (_, _) => messageStore.Clear();
        editContext.OnFieldChanged += (_, args) => ClearValidationMessages(args.FieldIdentifier);
    }

    private void ClearValidationMessages(FieldIdentifier field)
    {
        if (!editContext.GetValidationMessages(field).Any())
        {
            return;
        }

        messageStore.Clear(field);
        editContext.NotifyValidationStateChanged();
    }

    private async Task OpenPathDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] = FileSystemRootPaths,
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = FormModel.Path,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectArchiveStorageFolder"],
                Description = L["SelectArchiveStorageFolderDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (result.Cancelled)
        {
            return;
        }

        var selectedPath = result.GetData<string>();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            FormModel.Path = selectedPath;
            ClearValidationMessages(editContext.Field(nameof(FormModel.Path)));
        }
    }

    private async Task SaveAsync()
    {
        isSaving = true;

        var input = FormModel.ToInput();

        ArchiveStorageFolderSaveResult result;

        try
        {
            result = FormModel.ArchiveStorageFolderId is { } archiveStorageFolderId
                ? await operationRunner.RunAsync(
                    (ArchiveStorageFolderService service) =>
                        service.UpdateAsync(archiveStorageFolderId, input)
                )
                : await operationRunner.RunAsync(
                    (ArchiveStorageFolderService service) => service.CreateAsync(input)
                );
        }
        finally
        {
            isSaving = false;
        }

        if (!result.IsSuccess)
        {
            ShowValidationErrors(result.ValidationErrors);
            return;
        }

        await DialogRef.CloseAsync(DialogResult.Ok(result.ArchiveStorageFolderId));
    }

    private void ShowValidationErrors(
        IReadOnlyList<ArchiveStorageFolderValidationError> validationErrors
    )
    {
        messageStore.Clear();

        foreach (var validationError in validationErrors)
        {
            messageStore.Add(
                editContext.Field(GetFieldName(validationError)),
                L[$"ArchiveStorageFolderValidationError.{validationError}"]
            );
        }

        editContext.NotifyValidationStateChanged();
    }

    private static string GetFieldName(ArchiveStorageFolderValidationError validationError)
    {
        return validationError switch
        {
            ArchiveStorageFolderValidationError.NameRequired
            or ArchiveStorageFolderValidationError.NameTooLong
            or ArchiveStorageFolderValidationError.NameAlreadyExists => nameof(
                ArchiveStorageFolderFormModel.Name
            ),
            ArchiveStorageFolderValidationError.MinimumFreeSpaceNegative => nameof(
                ArchiveStorageFolderFormModel.MinimumFreeSpaceGb
            ),
            _ => nameof(ArchiveStorageFolderFormModel.Path),
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
