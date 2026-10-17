using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.FirstStartSetup.ArchiveOutputFolders;
using Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ArchiveConfigTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigTemplates;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;
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
    private const string DefaultReleaseGroupName = "Default";
    private const string RarArchiveFileExtension = ".rar";
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
    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private IReadOnlyList<ArchiverDto> archivers = [];
    private IReadOnlyList<HosterRegistrationReadModel> selectedArchiveHosterRegistrations = [];
    private List<SelectOption<int?>> releaseTemplateOptions = [];
    private List<SelectOption<int?>> releaseGroupOptions = [];
    private List<SelectOption<string>> archiverOptions = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool useExistingTemplate;
    private bool archiveOutputFolderExists;
    private bool isLoading = true;
    private bool isSubmitting;

    private ReleaseGroupReadModel? SelectedReleaseGroup =>
        releaseGroups.FirstOrDefault(releaseGroup =>
            releaseGroup.ReleaseGroupId == formModel.ReleaseGroupId
        );

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;

        releaseTemplates = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) => repository.GetAllAsync()
        );

        releaseGroups = await operationRunner.RunAsync(
            (IReleaseGroupReadRepository repository) => repository.GetAllAsync()
        );

        var hosterRegistrations = await operationRunner.RunAsync(
            (IHosterConfigurationReadRepository repository) => repository.GetAllRegistrationsAsync()
        );

        selectedArchiveHosterRegistrations = hosterRegistrations
            .Where(hosterRegistration =>
                WizardState.SelectedHosterRegistrationIds.Contains(hosterRegistration.Id)
                && !hosterRegistration.IsVideoHoster
            )
            .ToList();

        archivers = operationRunner.Run((IArchiverFactory factory) => factory.GetArchivers());

        releaseTemplateOptions = releaseTemplates
            .Select(releaseTemplate => new SelectOption<int?>(
                releaseTemplate.ReleaseTemplateId,
                releaseTemplate.Name
            ))
            .ToList();

        releaseGroupOptions = releaseGroups
            .Select(releaseGroup => new SelectOption<int?>(
                releaseGroup.ReleaseGroupId,
                releaseGroup.Name
            ))
            .ToList();

        archiverOptions = archivers
            .Select(archiver => new SelectOption<string>(archiver.ClassName, archiver.Name))
            .ToList();

        useExistingTemplate = releaseTemplates.Count > 0;
        formModel.ExistingReleaseTemplateId = WizardState.ReleaseTemplateId;

        formModel.ReleaseGroupId = (
            releaseGroups.FirstOrDefault(releaseGroup =>
                releaseGroup.Name == DefaultReleaseGroupName
            ) ?? releaseGroups.FirstOrDefault()
        )?.ReleaseGroupId;

        formModel.ReleaseTemplateName = L["FirstStartSetupDefaultReleaseTemplateName"];
        formModel.ArchiverName = archivers
            .Single(archiver => archiver.FileExtension == RarArchiveFileExtension)
            .ClassName;

        formModel.ArchiveFileSizeMb =
            selectedArchiveHosterRegistrations.Min(hosterRegistration =>
                hosterRegistration.MaxFileSizeMb
            ) ?? 0;

        formModel.ArchiveOutputFolderPath = operationRunner.Run(
            (FirstStartArchiveOutputFolderService service) =>
                service.GetSuggestedArchiveOutputFolderPath()
        );

        CheckArchiveOutputFolderExists();

        isLoading = false;
    }

    private void SelectUseExistingTemplate(bool useExisting)
    {
        useExistingTemplate = useExisting;
        messageStore.Clear();
        editContext.NotifyValidationStateChanged();
    }

    private void CheckArchiveOutputFolderExists()
    {
        ClearArchiveOutputFolderValidationMessages();

        archiveOutputFolderExists =
            !string.IsNullOrWhiteSpace(formModel.ArchiveOutputFolderPath)
            && operationRunner.Run(
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
        catch (Exception exception)
            when (exception
                    is ArchiveOutputFolderNotInsideExistingWorkingDirectoryException
                        or IOException
                        or UnauthorizedAccessException
            )
        {
            messageStore.Add(() => formModel.ArchiveOutputFolderPath, exception.Message);
            editContext.NotifyValidationStateChanged();
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

        var selectedPath = result.Cancelled ? null : result.GetData<string>();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        formModel.ArchiveOutputFolderPath = selectedPath;
        CheckArchiveOutputFolderExists();
    }

    private async Task SubmitAsync()
    {
        if (useExistingTemplate)
        {
            WizardState.ReleaseTemplateId = formModel.ExistingReleaseTemplateId;
            await OnNext.InvokeAsync();
            return;
        }

        isSubmitting = true;

        try
        {
            WizardState.ReleaseTemplateId = await CreateReleaseTemplateAsync();
        }
        finally
        {
            isSubmitting = false;
        }

        toastService.Success(
            L["FirstStartSetupReleaseTemplateCreated", formModel.ReleaseTemplateName]
        );
        await OnNext.InvokeAsync();
    }

    private async Task<int> CreateReleaseTemplateAsync()
    {
        var releaseTemplateId = await operationRunner.RunAsync(
            (ReleaseTemplateService service) =>
                service.CreateAsync(
                    formModel.ReleaseTemplateName.Trim(),
                    ReleaseType.Managed,
                    ReleaseContentType.Movie,
                    formModel.ReleaseGroupId!.Value
                )
        );

        var archiveConfigTemplateSettings = new ArchiveConfigTemplateSettings(
            Name: archivers.Single(archiver => archiver.ClassName == formModel.ArchiverName).Name,
            ArchiveFilesBasePath: formModel.ArchiveOutputFolderPath,
            ArchiverName: formModel.ArchiverName!,
            ArchivePassword: formModel.ArchivePassword,
            ArchiveFileSizeMb: formModel.ArchiveFileSizeMb,
            UseReleaseNameAsArchiveName: false,
            PackReleaseFolderAsRootFolder: true,
            CreateNonceFile: true,
            AdditionalArchiveContentIds: []
        );

        var archiveConfigTemplateResult = await operationRunner.RunAsync(
            (ArchiveConfigTemplateService service) =>
                service.CreateAsync(releaseTemplateId, archiveConfigTemplateSettings)
        );

        var archiveConfigTemplateId = archiveConfigTemplateResult.ArchiveConfigTemplateId!.Value;

        foreach (var hosterRegistration in selectedArchiveHosterRegistrations)
        {
            var uploadConfigTemplateSettings = new UploadConfigTemplateSettings(
                Name: null,
                UploadTarget: UploadTarget.ForHosterRegistration(hosterRegistration.Id),
                ArchiveConfigTemplateId: archiveConfigTemplateId,
                PremiumOnlyDownload: false
            );

            await operationRunner.RunAsync(
                (UploadConfigTemplateService service) =>
                    service.CreateAsync(
                        releaseTemplateId,
                        UploadSource.ArchiveFiles,
                        uploadConfigTemplateSettings
                    )
            );
        }

        return releaseTemplateId;
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (useExistingTemplate)
        {
            if (formModel.ExistingReleaseTemplateId is null)
            {
                messageStore.Add(
                    () => formModel.ExistingReleaseTemplateId!,
                    L["FirstStartSetupSelectReleaseTemplateRequired"]
                );
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(formModel.ReleaseTemplateName))
        {
            messageStore.Add(() => formModel.ReleaseTemplateName, L["NameIsRequired"]);
        }

        if (formModel.ArchiverName is null)
        {
            messageStore.Add(() => formModel.ArchiverName!, L["SelectArchiverRequired"]);
        }

        if (
            formModel.ArchiveFileSizeMb == 0
            && archivers.Any(archiver =>
                archiver.ClassName == formModel.ArchiverName
                && archiver.FileExtension == SevenZipArchiveFileExtension
            )
        )
        {
            messageStore.Add(
                () => formModel.ArchiveFileSizeMb,
                L["FirstStartSetupSevenZipRequiresArchivePartSize"]
            );
        }

        if (!archiveOutputFolderExists)
        {
            messageStore.Add(
                () => formModel.ArchiveOutputFolderPath,
                L["FirstStartSetupArchiveOutputFolderMustExist"]
            );
        }
    }

    private void ClearArchiveOutputFolderValidationMessages()
    {
        messageStore.Clear(editContext.Field(nameof(formModel.ArchiveOutputFolderPath)));
        editContext.NotifyValidationStateChanged();
    }
}
