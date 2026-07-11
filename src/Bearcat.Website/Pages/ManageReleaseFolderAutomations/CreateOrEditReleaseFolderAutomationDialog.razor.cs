using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageReleaseFolderAutomations;

public partial class CreateOrEditReleaseFolderAutomationDialog(
    DialogService dialogService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public ReleaseFolderAutomationFormModel FormModel { get; set; } = null!;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<ReleaseTemplateSummaryReadModel> releaseTemplates = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;

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
        releaseTemplates = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task SaveAsync()
    {
        if (FormModel.IsEdit && FormModel.ReleaseFolderAutomationId is not null)
        {
            await operationRunner.RunAsync(
                (ReleaseFolderAutomationService service) =>
                    service.UpdateAsync(
                        FormModel.ReleaseFolderAutomationId.Value,
                        FormModel.BasePath,
                        FormModel.FolderNamePattern,
                        FormModel.ReleaseTemplateId!.Value,
                        FormModel.PrimaryLanguageCode,
                        FormModel.IsEnabled
                    )
            );
        }
        else
        {
            await operationRunner.RunAsync(
                (ReleaseFolderAutomationService service) =>
                    service.CreateAsync(
                        FormModel.BasePath,
                        FormModel.FolderNamePattern,
                        FormModel.ReleaseTemplateId!.Value,
                        FormModel.PrimaryLanguageCode,
                        FormModel.IsEnabled
                    )
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task OpenFolderDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = FormModel.BasePath,
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
            FormModel.BasePath = selectedPath;
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.BasePath))
        {
            messageStore.Add(() => FormModel.BasePath, L["BasePathRequired"]);
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
