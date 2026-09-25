using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Assignment;
using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageArchiveConfigs;

public partial class CreateOrEditArchiveConfigDialog(
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    DialogService dialogService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public ArchiveConfigFormModel FormModel { get; set; } = null!;

    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? ArchiveConfigId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<ArchiverDto> archivers = [];
    private ArchiverDto? SelectedArchiver =>
        archivers.FirstOrDefault(a => a.ClassName == FormModel.ArchiverName);
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isEdit;

    protected override void OnInitialized()
    {
        archivers = operationRunner.Run((IArchiverFactory factory) => factory.GetArchivers());
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isEdit = ArchiveConfigId is not null;
    }

    private async Task SaveAsync()
    {
        var result = await operationRunner.RunAsync(
            (ArchiveConfigService service) =>
                isEdit
                    ? service.UpdateAsync(
                        archiveConfigId: ArchiveConfigId!.Value,
                        name: FormModel.Name!,
                        archiveFilesBasePath: FormModel.ArchiveFilesBasePath!,
                        archiveNamePrefix: FormModel.ArchiveNamePrefix!,
                        archivePassword: FormModel.ArchivePassword,
                        archiveFileSizeMb: FormModel.ArchiveFileSizeMb,
                        additionalArchiveContentIds: FormModel.GetAdditionalArchiveContentIds()
                    )
                    : service.CreateAsync(
                        releaseId: ReleaseId,
                        name: FormModel.Name!,
                        archiveFilesBasePath: FormModel.ArchiveFilesBasePath!,
                        archiverName: FormModel.ArchiverName!,
                        archiveNamePrefix: FormModel.ArchiveNamePrefix!,
                        archivePassword: FormModel.ArchivePassword,
                        archiveFileSizeMb: FormModel.ArchiveFileSizeMb,
                        additionalArchiveContentIds: FormModel.GetAdditionalArchiveContentIds()
                    )
        );

        if (!result.IsSuccess)
        {
            ShowDuplicatedAdditionalArchiveContentEntryNames(
                result.AdditionalArchiveContentEntryNameCollisions
            );
            return;
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void ShowDuplicatedAdditionalArchiveContentEntryNames(
        IReadOnlyList<DuplicatedEntryName> duplicatedEntryNames
    )
    {
        var field = editContext.Field(nameof(FormModel.AdditionalArchiveContentIds));
        messageStore.Clear(field);

        foreach (var duplicatedEntryName in duplicatedEntryNames)
        {
            messageStore.Add(field, L.Localize(duplicatedEntryName));
        }

        editContext.NotifyValidationStateChanged();
    }

    private void ClearAdditionalArchiveContentValidationMessages()
    {
        messageStore.Clear(editContext.Field(nameof(FormModel.AdditionalArchiveContentIds)));
        editContext.NotifyValidationStateChanged();
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

        if (!result.Cancelled)
        {
            var selectedPath = result.GetData<string>();
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                FormModel.ArchiveFilesBasePath = selectedPath;
            }
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name!, L["NameIsRequired"]);
        }

        if (FormModel.ArchiverName is null)
        {
            messageStore.Add(() => FormModel.ArchiverName!, L["SelectArchiverRequired"]);
        }

        if (string.IsNullOrWhiteSpace(FormModel.ArchiveFilesBasePath))
        {
            messageStore.Add(() => FormModel.ArchiveFilesBasePath!, L["BasePathRequired"]);
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
