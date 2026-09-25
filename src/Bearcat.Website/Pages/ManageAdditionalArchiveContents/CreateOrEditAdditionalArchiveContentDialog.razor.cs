using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Validation;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageAdditionalArchiveContents;

public partial class CreateOrEditAdditionalArchiveContentDialog(
    DialogService dialogService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public AdditionalArchiveContentFormModel FormModel { get; set; } = new();

    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isSaving;

    private IReadOnlyList<SelectOption<AdditionalArchiveContentType>> TypeOptions =>
        Enum.GetValues<AdditionalArchiveContentType>()
            .Select(type => new SelectOption<AdditionalArchiveContentType>(type, L.Localize(type)))
            .ToList();

    protected override void OnInitialized()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += (_, _) => messageStore.Clear();
        editContext.OnFieldChanged += (_, args) => ClearValidationMessages(args.FieldIdentifier);
    }

    private void NotifyTextContentChanged()
    {
        editContext.NotifyFieldChanged(editContext.Field(nameof(FormModel.TextContent)));
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

    private async Task OpenSourcePathDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = FormModel.SourcePath,
            [nameof(FolderSelectionDialog.IncludeFiles)] = true,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectFileOrFolder"],
                Description = L["SelectAdditionalArchiveContentSourceDescription"],
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
            FormModel.SourcePath = selectedPath;
            ClearValidationMessages(editContext.Field(nameof(FormModel.SourcePath)));
        }
    }

    private async Task SaveAsync()
    {
        isSaving = true;

        var input = FormModel.ToInput();

        AdditionalArchiveContentSaveResult result;

        try
        {
            result = FormModel.AdditionalArchiveContentId is { } additionalArchiveContentId
                ? await operationRunner.RunAsync(
                    (AdditionalArchiveContentService service) =>
                        service.UpdateAsync(additionalArchiveContentId, input)
                )
                : await operationRunner.RunAsync(
                    (AdditionalArchiveContentService service) => service.CreateAsync(input)
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

        await DialogRef.CloseAsync(DialogResult.Ok(result.AdditionalArchiveContentId));
    }

    private void ShowValidationErrors(
        IReadOnlyList<AdditionalArchiveContentValidationError> validationErrors
    )
    {
        messageStore.Clear();

        foreach (var validationError in validationErrors)
        {
            messageStore.Add(
                editContext.Field(GetFieldName(validationError)),
                L[$"AdditionalArchiveContentValidationError.{validationError}"]
            );
        }

        editContext.NotifyValidationStateChanged();
    }

    private static string GetFieldName(AdditionalArchiveContentValidationError validationError)
    {
        return validationError switch
        {
            AdditionalArchiveContentValidationError.NameRequired
            or AdditionalArchiveContentValidationError.NameTooLong
            or AdditionalArchiveContentValidationError.NameAlreadyExists => nameof(
                AdditionalArchiveContentFormModel.Name
            ),
            AdditionalArchiveContentValidationError.SourcePathRequired
            or AdditionalArchiveContentValidationError.SourcePathTooLong
            or AdditionalArchiveContentValidationError.SourcePathNotFound => nameof(
                AdditionalArchiveContentFormModel.SourcePath
            ),
            AdditionalArchiveContentValidationError.FileNameRequired
            or AdditionalArchiveContentValidationError.FileNameTooLong
            or AdditionalArchiveContentValidationError.FileNameInvalid
            or AdditionalArchiveContentValidationError.FileNameReserved => nameof(
                AdditionalArchiveContentFormModel.FileName
            ),
            _ => nameof(AdditionalArchiveContentFormModel.TextContent),
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
