using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateOrEditArchiveConfigTemplateDialog(
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    DialogService dialogService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public ArchiveConfigTemplateFormModel FormModel { get; set; } = null!;

    [Parameter]
    public int ReleaseTemplateId { get; set; }

    [Parameter]
    public int? ArchiveConfigTemplateId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<ArchiverDto> archivers = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isEdit;

    private ArchiverDto? SelectedArchiver =>
        archivers.FirstOrDefault(archiver => archiver.ClassName == FormModel.ArchiverName);

    protected override void OnInitialized()
    {
        archivers = operationRunner.Run((IArchiverFactory factory) => factory.GetArchivers());
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isEdit = ArchiveConfigTemplateId is not null;
    }

    private void SetDefaultArchiveConfigName()
    {
        if (!string.IsNullOrWhiteSpace(FormModel.Name))
        {
            return;
        }

        FormModel.Name = SelectedArchiver?.Name ?? string.Empty;
    }

    private async Task SaveAsync()
    {
        await operationRunner.RunAsync<ReleaseTemplateService>(async service =>
        {
            if (isEdit)
            {
                await service.UpdateArchiveConfigTemplateAsync(
                    ArchiveConfigTemplateId!.Value,
                    FormModel.Name,
                    FormModel.ArchiveFilesBasePath,
                    FormModel.ArchiverName!,
                    FormModel.ArchivePassword,
                    FormModel.ArchiveFileSizeMb,
                    FormModel.UseReleaseNameAsArchiveName
                );
                return;
            }

            await service.CreateArchiveConfigTemplateAsync(
                ReleaseTemplateId,
                FormModel.Name,
                FormModel.ArchiveFilesBasePath,
                FormModel.ArchiverName!,
                FormModel.ArchivePassword,
                FormModel.ArchiveFileSizeMb,
                FormModel.UseReleaseNameAsArchiveName
            );
        });

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task OpenFolderDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = FormModel.ArchiveFilesBasePath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectArchiveFolder"],
                Description = L["SelectArchiveFolderDescription"],
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
            FormModel.ArchiveFilesBasePath = selectedPath;
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, L["NameIsRequired"]);
        }

        if (FormModel.ArchiverName is null)
        {
            messageStore.Add(() => FormModel.ArchiverName!, L["SelectArchiverRequired"]);
        }

        if (string.IsNullOrWhiteSpace(FormModel.ArchiveFilesBasePath))
        {
            messageStore.Add(() => FormModel.ArchiveFilesBasePath, L["BasePathRequired"]);
        }

        if (FormModel.ArchiveFileSizeMb < 0)
        {
            messageStore.Add(
                () => FormModel.ArchiveFileSizeMb,
                L["ArchiveFileSizeMustBeZeroOrGreater"]
            );
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
