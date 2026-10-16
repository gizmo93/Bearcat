using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;
using Bearcat.Domain.UseCases.FirstStartSetup.Sources;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Preview;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupLocalFolderSource(
    DialogService dialogService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ReleaseTemplateSummaryReadModel ReleaseTemplate { get; set; } = null!;

    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback<FirstStartSetupCreatedSource> OnSourceCreated { get; set; }

    private readonly FirstStartSetupLocalFolderSourceFormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private ReleaseFolderAutomationPreview? preview;
    private List<ReleaseFolderAutomationPreviewFolder> unusedFolders = [];
    private List<SelectOption<string>> unusedFolderOptions = [];
    private bool isSubmitting;

    private bool IsManagedReleaseTemplate => ReleaseTemplate.ReleaseType is ReleaseType.Managed;

    private bool CanUploadTestFolder =>
        !(IsManagedReleaseTemplate && formModel.ExtractArchivesBeforeReleaseCreation);

    private FirstStartExistingFolderHandling SelectedExistingFolderHandling
    {
        get
        {
            if (unusedFolders.Count == 0)
            {
                return FirstStartExistingFolderHandling.IgnoreExisting;
            }

            if (
                !CanUploadTestFolder
                && formModel.ExistingFolderHandling
                    is FirstStartExistingFolderHandling.UploadTestFolderAndIgnoreOthers
            )
            {
                return FirstStartExistingFolderHandling.IgnoreExisting;
            }

            return formModel.ExistingFolderHandling;
        }
    }

    protected override void OnInitialized()
    {
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        editContext.OnFieldChanged += (_, args) => messageStore.Clear(args.FieldIdentifier);
    }

    private void HandlePreviewLoaded(ReleaseFolderAutomationPreview? loadedPreview)
    {
        preview = loadedPreview;
        unusedFolders =
            loadedPreview
                ?.MatchingFolders.Where(folder => !folder.IsUsedByReleaseArchiveOrRemoteDownload)
                .ToList()
            ?? [];
        unusedFolderOptions = unusedFolders
            .Select(folder => new SelectOption<string>(folder.FolderPath, folder.FolderName))
            .ToList();

        if (unusedFolders.All(folder => folder.FolderPath != formModel.TestFolderPath))
        {
            formModel.TestFolderPath = unusedFolders.FirstOrDefault()?.FolderPath;
        }
    }

    private string GetChoiceClass(FirstStartExistingFolderHandling existingFolderHandling)
    {
        return FirstStartSetupChoiceCardStyle.GetClass(
            SelectedExistingFolderHandling == existingFolderHandling
        );
    }

    private string IsCheckedAttributeValue(FirstStartExistingFolderHandling existingFolderHandling)
    {
        return SelectedExistingFolderHandling == existingFolderHandling ? "true" : "false";
    }

    private void SelectExistingFolderHandling(
        FirstStartExistingFolderHandling existingFolderHandling
    )
    {
        formModel.ExistingFolderHandling = existingFolderHandling;
        messageStore.Clear(editContext.Field(nameof(formModel.TestFolderPath)));
        editContext.NotifyValidationStateChanged();
    }

    private async Task OpenFolderDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = formModel.BasePath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectReleaseBaseFolder"],
                Description = L["SelectReleaseBaseFolderDescription"],
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
            formModel.BasePath = selectedPath;
            messageStore.Clear(editContext.Field(nameof(formModel.BasePath)));
            editContext.NotifyValidationStateChanged();
        }
    }

    private async Task SubmitAsync()
    {
        var existingFolderHandling = SelectedExistingFolderHandling;
        var input = new FirstStartFolderSourceInput(
            BasePath: formModel.BasePath,
            FolderNamePattern: formModel.FolderNamePattern,
            ReleaseTemplateId: ReleaseTemplate.ReleaseTemplateId,
            ExtractArchivesBeforeReleaseCreation: IsManagedReleaseTemplate
                && formModel.ExtractArchivesBeforeReleaseCreation,
            ExistingFolderHandling: existingFolderHandling,
            TestFolderPath: existingFolderHandling
            is FirstStartExistingFolderHandling.UploadTestFolderAndIgnoreOthers
                ? formModel.TestFolderPath
                : null
        );

        isSubmitting = true;
        FirstStartFolderSourceResult result;

        try
        {
            result = await operationRunner.RunAsync(
                (FirstStartFolderSourceService service) => service.CreateFolderSourceAsync(input)
            );
        }
        catch (ArchiveOutputFolderInsideSourceFolderException exception)
        {
            messageStore.Add(
                () => formModel.BasePath,
                L["ArchiveOutputFolderInsideSourceFolder", exception.ArchiveOutputFolderPath]
            );
            editContext.NotifyValidationStateChanged();
            return;
        }
        finally
        {
            isSubmitting = false;
        }

        await OnSourceCreated.InvokeAsync(
            new FirstStartSetupCreatedSource(
                FirstStartSetupSourceKind.LocalFolder,
                RemoteSourceAutomationName: null,
                FolderPath: formModel.BasePath.Trim(),
                result.TestReleaseId,
                result.TestReleaseInitialUploadCreationResult
            )
        );
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.BasePath))
        {
            messageStore.Add(() => formModel.BasePath, L["BasePathRequired"]);
        }
        else if (preview is not { BasePathExists: true })
        {
            messageStore.Add(() => formModel.BasePath, L["ReleaseFolderPreviewBasePathMissing"]);
        }

        if (
            SelectedExistingFolderHandling
                is FirstStartExistingFolderHandling.UploadTestFolderAndIgnoreOthers
            && formModel.TestFolderPath is null
        )
        {
            messageStore.Add(
                () => formModel.TestFolderPath!,
                L["FirstStartSetupSelectTestFolderRequired"]
            );
        }
    }
}
