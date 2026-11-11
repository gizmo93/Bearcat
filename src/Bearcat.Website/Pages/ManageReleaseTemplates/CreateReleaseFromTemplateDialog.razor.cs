using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Preview;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Exceptions;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Website.Pages.ManageReleases;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateReleaseFromTemplateDialog(
    DialogService dialogService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    NavigationManager navigationManager,
    IScopedOperationRunner operationRunner,
    ToastService toastService
) : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private CreateReleaseFromTemplateFormModel formModel = new();
    private IReadOnlyList<ReleaseTemplateSummaryReadModel> releaseTemplates = [];
    private ReleaseTemplateDetailReadModel? selectedTemplate;
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private string? folderValidationMessage;
    private bool isCreating;
    private ReleaseFolderAutomationPreview? subfolderPreview;
    private readonly HashSet<string> selectedSubfolderPaths = [];
    private readonly HashSet<string> createdSubfolderPaths = [];
    private readonly Dictionary<string, string> subfolderErrorMessagesByFolderPath = [];
    private int subfolderCountToCreate;
    private int processedSubfolderCount;

    private IReadOnlyList<SelectOption<int?>> ReleaseTemplateOptions =>
        releaseTemplates
            .Select(template => new SelectOption<int?>(template.ReleaseTemplateId, template.Name))
            .ToList();

    private IReadOnlyList<SelectOption<string>> LanguageOptions =>
        PrimaryLanguageSelectOptions.Create(L["Unknown"]);

    private string SubmitButtonText
    {
        get
        {
            if (!formModel.CreateOneReleasePerSubfolder)
            {
                return L["CreateRelease"];
            }

            if (isCreating)
            {
                return L[
                    "CreatingReleasesProgress",
                    Math.Min(processedSubfolderCount + 1, subfolderCountToCreate),
                    subfolderCountToCreate
                ];
            }

            return L["CreateReleasesCount", selectedSubfolderPaths.Count];
        }
    }

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        releaseTemplates = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task HandleTemplateChangedAsync()
    {
        selectedTemplate = formModel.ReleaseTemplateId is null
            ? null
            : await operationRunner.RunAsync(
                (IReleaseTemplateReadRepository repository) =>
                    repository.GetDetailAsync(formModel.ReleaseTemplateId.Value)
            );
    }

    private async Task SaveAsync()
    {
        if (isCreating)
        {
            return;
        }

        if (formModel.CreateOneReleasePerSubfolder)
        {
            await CreateReleasesFromSelectedSubfoldersAsync();
            return;
        }

        isCreating = true;
        int releaseId;

        try
        {
            releaseId = await operationRunner.RunAsync(
                (ReleaseService service) =>
                    service.CreateFromTemplateAsync(
                        releaseTemplateId: formModel.ReleaseTemplateId!.Value,
                        releaseFolderPath: formModel.FolderPath,
                        name: formModel.Name,
                        primaryLanguageCode: formModel.PrimaryLanguageCode
                    )
            );
        }
        catch (ReleaseFolderNotFoundException)
        {
            ShowFolderValidationMessage(L["SelectedReleaseFolderNotFound"]);
            return;
        }
        catch (ReleaseFolderAlreadyInUseException exception)
        {
            ShowFolderValidationMessage(GetFolderAlreadyInUseMessage(exception.UsageKind));
            return;
        }
        catch (ReleaseTemplateNotFoundException)
        {
            messageStore.Add(
                () => formModel.ReleaseTemplateId!,
                L["SelectedReleaseTemplateNotFound"]
            );
            editContext.NotifyValidationStateChanged();
            return;
        }
        finally
        {
            isCreating = false;
        }

        await DialogRef.CloseAsync(DialogResult.Ok(releaseId));
        navigationManager.NavigateTo($"/releases/{releaseId}");
    }

    private async Task CreateReleasesFromSelectedSubfoldersAsync()
    {
        var folderPaths = subfolderPreview!
            .MatchingFolders.Select(subfolder => subfolder.FolderPath)
            .Where(selectedSubfolderPaths.Contains)
            .ToList();

        isCreating = true;
        subfolderCountToCreate = folderPaths.Count;
        processedSubfolderCount = 0;
        subfolderErrorMessagesByFolderPath.Clear();

        try
        {
            foreach (var folderPath in folderPaths)
            {
                StateHasChanged();
                await CreateReleaseFromSubfolderAsync(folderPath);
                processedSubfolderCount++;
            }
        }
        catch (ReleaseTemplateNotFoundException)
        {
            messageStore.Add(
                () => formModel.ReleaseTemplateId!,
                L["SelectedReleaseTemplateNotFound"]
            );

            editContext.NotifyValidationStateChanged();
            return;
        }
        finally
        {
            isCreating = false;
        }

        if (subfolderErrorMessagesByFolderPath.Count > 0)
        {
            return;
        }

        toastService.Success(L["ReleasesCreatedCount", createdSubfolderPaths.Count]);
        await DialogRef.CloseAsync(DialogResult.Ok(createdSubfolderPaths.Count));
    }

    private async Task CreateReleaseFromSubfolderAsync(string folderPath)
    {
        try
        {
            await operationRunner.RunAsync(
                (ReleaseService service) =>
                    service.CreateFromTemplateAsync(
                        releaseTemplateId: formModel.ReleaseTemplateId!.Value,
                        releaseFolderPath: folderPath,
                        name: null,
                        primaryLanguageCode: formModel.PrimaryLanguageCode
                    )
            );

            createdSubfolderPaths.Add(folderPath);
            selectedSubfolderPaths.Remove(folderPath);
        }
        catch (ReleaseFolderNotFoundException)
        {
            subfolderErrorMessagesByFolderPath[folderPath] = L["SelectedReleaseFolderNotFound"];
        }
        catch (ReleaseFolderAlreadyInUseException exception)
        {
            subfolderErrorMessagesByFolderPath[folderPath] = GetFolderAlreadyInUseMessage(
                exception.UsageKind
            );
        }
    }

    private async Task HandleCreateOneReleasePerSubfolderChangedAsync()
    {
        if (
            formModel.CreateOneReleasePerSubfolder
            && !string.IsNullOrWhiteSpace(formModel.FolderPath)
        )
        {
            await LoadSubfoldersAsync();
        }
    }

    private async Task LoadSubfoldersAsync()
    {
        subfolderPreview = await operationRunner.RunAsync(
            (ReleaseFolderAutomationPreviewService service) =>
                service.PreviewMatchingFoldersAsync(formModel.FolderPath, null)
        );

        createdSubfolderPaths.Clear();
        subfolderErrorMessagesByFolderPath.Clear();
        SelectAllSubfoldersThatCanBecomeReleases();
    }

    private void SelectAllSubfoldersThatCanBecomeReleases()
    {
        selectedSubfolderPaths.Clear();

        foreach (
            var subfolder in subfolderPreview!.MatchingFolders.Where(CanSubfolderBecomeRelease)
        )
        {
            selectedSubfolderPaths.Add(subfolder.FolderPath);
        }
    }

    private void DeselectAllSubfolders()
    {
        selectedSubfolderPaths.Clear();
    }

    private void SetSubfolderSelected(string folderPath, bool isSelected)
    {
        if (isSelected)
        {
            selectedSubfolderPaths.Add(folderPath);
            return;
        }

        selectedSubfolderPaths.Remove(folderPath);
    }

    private bool CanSubfolderBecomeRelease(ReleaseFolderAutomationPreviewFolder subfolder)
    {
        return !subfolder.IsUsedByReleaseArchiveOrRemoteDownload
            && !createdSubfolderPaths.Contains(subfolder.FolderPath);
    }

    private void ShowFolderValidationMessage(string message)
    {
        folderValidationMessage = message;
        messageStore.Add(() => formModel.FolderPath, message);
        editContext.NotifyValidationStateChanged();
    }

    private string GetFolderAlreadyInUseMessage(ReleaseFolderUsageKind usageKind)
    {
        return usageKind switch
        {
            ReleaseFolderUsageKind.ReleaseFolder => L["ReleaseFolderAlreadyUsedByRelease"],
            ReleaseFolderUsageKind.UnmanagedArchiveFolder => L[
                "ReleaseFolderAlreadyUsedByUnmanagedRelease"
            ],
            ReleaseFolderUsageKind.RemoteDownloadFolder => L[
                "ReleaseFolderAlreadyUsedByRemoteDownload"
            ],
            _ => throw new ArgumentOutOfRangeException(
                nameof(usageKind),
                $"Unknown release folder usage kind, {usageKind}"
            ),
        };
    }

    private async Task OpenFolderDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = formModel.FolderPath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectReleaseFolder"],
                Description = L["SelectReleaseFolderDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (result.Cancelled)
        {
            return;
        }

        var folderPath = result.GetData<string>();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        formModel.FolderPath = folderPath;
        folderValidationMessage = null;

        if (formModel.CreateOneReleasePerSubfolder)
        {
            await LoadSubfoldersAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            formModel.Name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        folderValidationMessage = null;
        messageStore.Clear();

        if (formModel.ReleaseTemplateId is null)
        {
            messageStore.Add(
                () => formModel.ReleaseTemplateId!,
                L["SelectReleaseTemplateRequired"]
            );
        }

        if (string.IsNullOrWhiteSpace(formModel.FolderPath))
        {
            folderValidationMessage = L["SelectFolderRequired"];
            messageStore.Add(() => formModel.FolderPath, folderValidationMessage);
        }
        else if (formModel.CreateOneReleasePerSubfolder && selectedSubfolderPaths.Count == 0)
        {
            folderValidationMessage = L["SelectAtLeastOneSubfolder"];
            messageStore.Add(() => formModel.FolderPath, folderValidationMessage);
        }

        if (!formModel.CreateOneReleasePerSubfolder && string.IsNullOrWhiteSpace(formModel.Name))
        {
            messageStore.Add(() => formModel.Name, L["NameIsRequired"]);
        }
    }

    private async Task CancelAsync()
    {
        if (createdSubfolderPaths.Count > 0)
        {
            await DialogRef.CloseAsync(DialogResult.Ok(createdSubfolderPaths.Count));
            return;
        }

        await DialogRef.CancelAsync();
    }
}
