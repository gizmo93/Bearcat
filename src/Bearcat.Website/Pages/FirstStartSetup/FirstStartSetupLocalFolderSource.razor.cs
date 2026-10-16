using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Preview;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;
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
    private const int MaxTestFolderOptionCount = 50;

    [Parameter]
    public int ReleaseTemplateId { get; set; }

    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback OnSourceCreated { get; set; }

    private readonly FirstStartSetupLocalFolderSourceFormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private List<ReleaseFolderAutomationPreviewFolder> unusedFolders = [];
    private string testFolderSearchQuery = string.Empty;
    private bool isManagedReleaseTemplate;
    private bool isSubmitting;
    private bool isSourceCreated;
    private int? testReleaseId;
    private InitialUploadCreationForReleaseResult? testReleaseUploadCreationResult;

    private bool ExtractsArchivesBeforeReleaseCreation =>
        isManagedReleaseTemplate && formModel.ExtractArchivesBeforeReleaseCreation;

    private bool CanUploadTestFolder => !ExtractsArchivesBeforeReleaseCreation;

    private FirstStartSetupExistingFolderHandling SelectedExistingFolderHandling
    {
        get
        {
            var isUnavailableTestUpload =
                !CanUploadTestFolder
                && formModel.ExistingFolderHandling
                    is FirstStartSetupExistingFolderHandling.UploadTestFolderAndIgnoreOthers;

            return unusedFolders.Count == 0 || isUnavailableTestUpload
                ? FirstStartSetupExistingFolderHandling.IgnoreExisting
                : formModel.ExistingFolderHandling;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;

        var releaseTemplate = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) =>
                repository.GetDetailAsync(ReleaseTemplateId)
        );
        isManagedReleaseTemplate = releaseTemplate!.ReleaseType is ReleaseType.Managed;
    }

    private void HandlePreviewLoaded(ReleaseFolderAutomationPreview? preview)
    {
        unusedFolders =
            preview
                ?.MatchingFolders.Where(folder => !folder.IsUsedByReleaseArchiveOrRemoteDownload)
                .ToList()
            ?? [];

        if (unusedFolders.All(folder => folder.FolderPath != formModel.TestFolderPath))
        {
            formModel.TestFolderPath = unusedFolders.FirstOrDefault()?.FolderPath;
        }
    }

    private List<SelectOption<string>> GetTestFolderOptions()
    {
        return unusedFolders
            .Where(folder =>
                folder.FolderName.Contains(
                    testFolderSearchQuery,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Take(MaxTestFolderOptionCount)
            .Select(folder => new SelectOption<string>(folder.FolderPath, folder.FolderName))
            .ToList();
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

        var selectedPath = result.Cancelled ? null : result.GetData<string>();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            formModel.BasePath = selectedPath;
        }
    }

    private async Task SubmitAsync()
    {
        var existingFolderHandling = SelectedExistingFolderHandling;
        isSubmitting = true;

        try
        {
            if (
                existingFolderHandling
                is FirstStartSetupExistingFolderHandling.UploadTestFolderAndIgnoreOthers
            )
            {
                testReleaseId = await operationRunner.RunAsync(
                    (ReleaseService service) =>
                        service.CreateFromTemplateAsync(
                            ReleaseTemplateId,
                            formModel.TestFolderPath!,
                            name: null,
                            primaryLanguageCode: null
                        )
                );
            }

            await operationRunner.RunAsync(
                (ReleaseFolderAutomationService service) =>
                    service.CreateAsync(
                        formModel.BasePath,
                        formModel.FolderNamePattern,
                        ReleaseTemplateId,
                        primaryLanguageCode: null,
                        extractArchivesBeforeReleaseCreation: ExtractsArchivesBeforeReleaseCreation,
                        isEnabled: true,
                        ignoreExistingOnFirstScan: existingFolderHandling
                            is not FirstStartSetupExistingFolderHandling.ImportExisting
                    )
            );

            if (testReleaseId is { } releaseId)
            {
                testReleaseUploadCreationResult = await operationRunner.RunAsync(
                    (UploadCreationService service) =>
                        service.CreateMissingInitialUploadsForReleaseIgnoringCooldownAsync(
                            releaseId
                        )
                );
            }
        }
        finally
        {
            isSubmitting = false;
        }

        isSourceCreated = true;
        await OnSourceCreated.InvokeAsync();
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.BasePath))
        {
            messageStore.Add(() => formModel.BasePath, L["BasePathRequired"]);
        }
    }
}
