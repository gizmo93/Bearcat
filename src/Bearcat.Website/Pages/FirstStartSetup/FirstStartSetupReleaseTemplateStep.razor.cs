using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.FirstStartSetup.ArchiveDefaults;
using Bearcat.Domain.UseCases.FirstStartSetup.ArchiveOutputFolders;
using Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;
using Bearcat.Domain.UseCases.FirstStartSetup.ReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupReleaseTemplateStep(
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    private const string SevenZipArchiveFileExtension = ".7z";

    [Parameter]
    [EditorRequired]
    public FirstStartSetupWizardState WizardState { get; set; } = null!;

    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback OnSkip { get; set; }

    [Parameter]
    public EventCallback OnNext { get; set; }

    private readonly FirstStartSetupReleaseTemplateFormModel formModel = new();
    private IReadOnlyList<ReleaseTemplateSummaryReadModel> releaseTemplates = [];
    private IReadOnlyList<ArchiverDto> archivers = [];
    private List<SelectOption<int?>> releaseTemplateOptions = [];
    private List<SelectOption<string>> archiverOptions = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private FirstStartSetupReleaseTemplateChoice choice =
        FirstStartSetupReleaseTemplateChoice.CreateNewTemplate;
    private bool archiveOutputFolderExists;
    private bool isLoading = true;
    private bool isSubmitting;

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;

        releaseTemplates = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) => repository.GetAllAsync()
        );
        archivers = operationRunner.Run((IArchiverFactory factory) => factory.GetArchivers());
        releaseTemplateOptions = releaseTemplates
            .Select(releaseTemplate => new SelectOption<int?>(
                releaseTemplate.ReleaseTemplateId,
                releaseTemplate.Name
            ))
            .ToList();
        archiverOptions = archivers
            .Select(archiver => new SelectOption<string>(archiver.ClassName, archiver.Name))
            .ToList();
        var archiveDefaults = await operationRunner.RunAsync(
            (FirstStartArchiveDefaultsService service) =>
                service.GetArchiveDefaultsAsync(WizardState.SelectedHosterRegistrationIds.ToList())
        );

        ApplyDefaults(archiveDefaults);
        isLoading = false;
    }

    private void ApplyDefaults(FirstStartArchiveDefaults archiveDefaults)
    {
        var defaultReleaseGroupName = L["FirstStartSetupDefaultReleaseGroupName"].Value;

        formModel.ReleaseGroupName = defaultReleaseGroupName;
        formModel.ReleaseTemplateName = defaultReleaseGroupName;
        formModel.ArchiverName = archiveDefaults.ArchiverName;
        formModel.ArchiveFileSizeMb = archiveDefaults.ArchiveFileSizeMb;
        formModel.ArchiveOutputFolderPath = archiveDefaults.SuggestedArchiveOutputFolderPath;
        archiveOutputFolderExists = archiveDefaults.SuggestedArchiveOutputFolderExists;

        if (releaseTemplates.Count == 0)
        {
            return;
        }

        choice = FirstStartSetupReleaseTemplateChoice.UseExistingTemplate;
        formModel.ExistingReleaseTemplateId = releaseTemplates.Any(releaseTemplate =>
            releaseTemplate.ReleaseTemplateId == WizardState.ReleaseTemplateId
        )
            ? WizardState.ReleaseTemplateId
            : null;
    }

    private string GetChoiceClass(FirstStartSetupReleaseTemplateChoice choiceToRender)
    {
        return FirstStartSetupChoiceCardStyle.GetClass(choice == choiceToRender);
    }

    private void SelectChoice(FirstStartSetupReleaseTemplateChoice selectedChoice)
    {
        choice = selectedChoice;
        messageStore.Clear();
        editContext.NotifyValidationStateChanged();
    }

    private void CheckArchiveOutputFolderExists()
    {
        ClearArchiveOutputFolderValidationMessages();

        if (string.IsNullOrWhiteSpace(formModel.ArchiveOutputFolderPath))
        {
            archiveOutputFolderExists = false;
            return;
        }

        archiveOutputFolderExists = operationRunner.Run(
            (IFileSystemService service) =>
                service.DirectoryExists(formModel.ArchiveOutputFolderPath)
        );
    }

    private void CreateArchiveOutputFolder()
    {
        ClearArchiveOutputFolderValidationMessages();

        try
        {
            operationRunner.Run(
                (FirstStartArchiveOutputFolderService service) =>
                    service.CreateArchiveOutputFolder(formModel.ArchiveOutputFolderPath)
            );
        }
        catch (ArchiveOutputFolderNotInsideExistingWorkingDirectoryException exception)
        {
            ShowArchiveOutputFolderValidationMessage(exception.Message);
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowArchiveOutputFolderValidationMessage(exception.Message);
            return;
        }

        archiveOutputFolderExists = true;
        toastService.Success(L["FirstStartSetupArchiveOutputFolderCreated"]);
    }

    private async Task OpenFolderDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = formModel.ArchiveOutputFolderPath,
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
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        formModel.ArchiveOutputFolderPath = selectedPath;
        CheckArchiveOutputFolderExists();
    }

    private async Task SubmitAsync()
    {
        if (choice is FirstStartSetupReleaseTemplateChoice.UseExistingTemplate)
        {
            WizardState.ReleaseTemplateId = formModel.ExistingReleaseTemplateId!.Value;
            await OnNext.InvokeAsync();
            return;
        }

        isSubmitting = true;
        int releaseTemplateId;

        try
        {
            var input = formModel.ToInput(WizardState.SelectedHosterRegistrationIds.ToList());
            releaseTemplateId = await operationRunner.RunAsync(
                (FirstStartReleaseTemplateService service) =>
                    service.CreateReleaseGroupAndTemplateAsync(input)
            );
        }
        catch (ArchiveOutputFolderNotFoundException)
        {
            archiveOutputFolderExists = false;
            ShowArchiveOutputFolderValidationMessage(
                L["FirstStartSetupArchiveOutputFolderMustExist"]
            );
            return;
        }
        finally
        {
            isSubmitting = false;
        }

        WizardState.ReleaseTemplateId = releaseTemplateId;
        toastService.Success(
            L["FirstStartSetupReleaseTemplateCreated", formModel.ReleaseTemplateName]
        );
        await OnNext.InvokeAsync();
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (choice is FirstStartSetupReleaseTemplateChoice.UseExistingTemplate)
        {
            ValidateExistingReleaseTemplateSelection();
            return;
        }

        ValidateNewReleaseTemplate();
    }

    private void ValidateExistingReleaseTemplateSelection()
    {
        if (formModel.ExistingReleaseTemplateId is null)
        {
            messageStore.Add(
                () => formModel.ExistingReleaseTemplateId!,
                L["FirstStartSetupSelectReleaseTemplateRequired"]
            );
        }
    }

    private void ValidateNewReleaseTemplate()
    {
        if (string.IsNullOrWhiteSpace(formModel.ReleaseGroupName))
        {
            messageStore.Add(() => formModel.ReleaseGroupName, L["NameIsRequired"]);
        }

        if (formModel.EnableAutomaticReuploads && formModel.NumberOfHoursUntilReupload < 0)
        {
            messageStore.Add(
                () => formModel.NumberOfHoursUntilReupload,
                L["HoursUntilReuploadMustBeZeroOrGreater"]
            );
        }

        if (string.IsNullOrWhiteSpace(formModel.ReleaseTemplateName))
        {
            messageStore.Add(() => formModel.ReleaseTemplateName, L["NameIsRequired"]);
        }

        if (formModel.ArchiverName is null)
        {
            messageStore.Add(() => formModel.ArchiverName!, L["SelectArchiverRequired"]);
        }

        ValidateArchiveFileSize();
        ValidateArchiveOutputFolder();
    }

    private void ValidateArchiveFileSize()
    {
        if (formModel.ArchiveFileSizeMb < 0)
        {
            messageStore.Add(
                () => formModel.ArchiveFileSizeMb,
                L["ArchiveFileSizeMustBeZeroOrGreater"]
            );
            return;
        }

        var selectedArchiver = archivers.FirstOrDefault(archiver =>
            archiver.ClassName == formModel.ArchiverName
        );

        if (
            selectedArchiver?.FileExtension == SevenZipArchiveFileExtension
            && formModel.ArchiveFileSizeMb == 0
        )
        {
            messageStore.Add(
                () => formModel.ArchiveFileSizeMb,
                L["FirstStartSetupSevenZipRequiresArchivePartSize"]
            );
        }
    }

    private void ValidateArchiveOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(formModel.ArchiveOutputFolderPath))
        {
            messageStore.Add(() => formModel.ArchiveOutputFolderPath, L["BasePathRequired"]);
            return;
        }

        if (!archiveOutputFolderExists)
        {
            messageStore.Add(
                () => formModel.ArchiveOutputFolderPath,
                L["FirstStartSetupArchiveOutputFolderMustExist"]
            );
        }
    }

    private void ShowArchiveOutputFolderValidationMessage(string message)
    {
        messageStore.Add(() => formModel.ArchiveOutputFolderPath, message);
        editContext.NotifyValidationStateChanged();
    }

    private void ClearArchiveOutputFolderValidationMessages()
    {
        messageStore.Clear(editContext.Field(nameof(formModel.ArchiveOutputFolderPath)));
        editContext.NotifyValidationStateChanged();
    }
}
