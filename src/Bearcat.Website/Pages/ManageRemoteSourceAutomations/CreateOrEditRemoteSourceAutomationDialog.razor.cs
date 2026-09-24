using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;
using Bearcat.Domain.UseCases.ManageRemoteSources.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageRemoteSourceAutomations;

public partial class CreateOrEditRemoteSourceAutomationDialog(
    DialogService dialogService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public RemoteSourceAutomationFormModel FormModel { get; set; } = null!;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<RemoteSourceRegistrationReadModel> registrations = [];
    private IReadOnlyList<ReleaseTemplateSummaryReadModel> releaseTemplates = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private string? errorMessage;
    private bool isSaving;
    private bool isInitialized;

    private IReadOnlyList<SelectOption<int?>> RegistrationOptions =>
        registrations
            .Select(registration => new SelectOption<int?>(registration.Id, registration.Name))
            .ToList();

    private IReadOnlyList<SelectOption<int?>> ReleaseTemplateOptions =>
        releaseTemplates
            .Select(template => new SelectOption<int?>(template.ReleaseTemplateId, template.Name))
            .ToList();

    private IReadOnlyList<SelectOption<string>> LanguageOptions =>
        [
            new(string.Empty, L["Unknown"]),
            .. CultureInfo
                .GetCultures(CultureTypes.NeutralCultures)
                .Where(culture => culture.TwoLetterISOLanguageName.Length == 2)
                .DistinctBy(culture => culture.TwoLetterISOLanguageName)
                .OrderBy(culture => culture.NativeName)
                .Select(culture => new SelectOption<string>(
                    culture.TwoLetterISOLanguageName,
                    culture.NativeName
                )),
        ];

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        editContext.OnFieldChanged += (_, args) => messageStore.Clear(args.FieldIdentifier);

        var allRegistrations = await operationRunner.RunAsync(
            (IRemoteSourceRegistrationReadRepository repository) => repository.GetAllAsync()
        );
        registrations = allRegistrations
            .Where(registration =>
                registration.IsActive || registration.Id == FormModel.RemoteSourceRegistrationId
            )
            .ToList();
        releaseTemplates = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) => repository.GetAllAsync()
        );
        isInitialized = true;
    }

    private ReleaseType? SelectedReleaseType =>
        releaseTemplates
            .FirstOrDefault(template => template.ReleaseTemplateId == FormModel.ReleaseTemplateId)
            ?.ReleaseType;

    private async Task OpenRemoteFolderDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.Source)] = new RemoteFolderSelectionSource(
                operationRunner,
                FormModel.RemoteSourceRegistrationId!.Value
            ),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = FormModel.RemotePath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectRemoteFolder"],
                Description = L["SelectRemoteFolderDescription"],
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
            FormModel.RemotePath = selectedPath;
        }
    }

    private async Task OpenTargetFolderDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = FormModel.TargetPath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectDownloadFolder"],
                Description = L["SelectDownloadFolderDescription"],
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
            FormModel.TargetPath = selectedPath;
        }
    }

    private async Task SaveAsync()
    {
        errorMessage = null;
        isSaving = true;

        var input = new RemoteSourceAutomationInput
        {
            Name = FormModel.Name,
            RemoteSourceRegistrationId = FormModel.RemoteSourceRegistrationId!.Value,
            RemotePath = FormModel.RemotePath,
            TargetPath = FormModel.TargetPath,
            FolderNamePattern = FormModel.FolderNamePattern,
            ReleaseTemplateId = FormModel.ReleaseTemplateId!.Value,
            PrimaryLanguageCode = FormModel.PrimaryLanguageCode,
            KeepRawFiles = SelectedReleaseType is not ReleaseType.Managed || FormModel.KeepRawFiles,
            Priority = FormModel.Priority,
            IgnoreExistingOnFirstScan = FormModel.IgnoreExistingOnFirstScan,
        };

        try
        {
            if (FormModel.RemoteSourceAutomationId is { } automationId)
            {
                await operationRunner.RunAsync(
                    (RemoteSourceAutomationService service) =>
                        service.UpdateAsync(automationId, input)
                );
            }
            else
            {
                await operationRunner.RunAsync(
                    (RemoteSourceAutomationService service) => service.CreateAsync(input)
                );
            }
        }
        catch (ArgumentException exception)
        {
            errorMessage = exception.Message;

            return;
        }
        finally
        {
            isSaving = false;
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, L["NameIsRequired"]);
        }

        if (FormModel.RemoteSourceRegistrationId is null)
        {
            messageStore.Add(
                () => FormModel.RemoteSourceRegistrationId!,
                L["SelectRemoteSourceRequired"]
            );
        }

        if (string.IsNullOrWhiteSpace(FormModel.RemotePath))
        {
            messageStore.Add(() => FormModel.RemotePath, L["RemotePathRequired"]);
        }

        if (string.IsNullOrWhiteSpace(FormModel.TargetPath))
        {
            messageStore.Add(() => FormModel.TargetPath, L["TargetPathRequired"]);
        }

        if (FormModel.ReleaseTemplateId is null)
        {
            messageStore.Add(
                () => FormModel.ReleaseTemplateId!,
                L["SelectReleaseTemplateRequired"]
            );
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
