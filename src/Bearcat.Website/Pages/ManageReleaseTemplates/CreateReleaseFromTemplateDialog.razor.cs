using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateReleaseFromTemplateDialog(
    IReleaseTemplateReadRepository readRepository,
    DialogService dialogService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    NavigationManager navigationManager
) : OwningComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private CreateReleaseFromTemplateFormModel formModel = new();
    private IReadOnlyList<ReleaseTemplateSummaryReadModel> releaseTemplates = [];
    private ReleaseTemplateDetailReadModel? selectedTemplate;
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private string? folderValidationMessage;

    private IEnumerable<SelectOption<int?>> ReleaseTemplateOptions =>
        releaseTemplates.Select(template => new SelectOption<int?>(
            template.ReleaseTemplateId,
            template.Name
        ));

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        releaseTemplates = await readRepository.GetAllAsync();
    }

    private async Task HandleTemplateChangedAsync()
    {
        selectedTemplate = formModel.ReleaseTemplateId is null
            ? null
            : await readRepository.GetDetailAsync(formModel.ReleaseTemplateId.Value);
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseService>();
        var releaseId = await service.CreateFromTemplateAsync(
            formModel.ReleaseTemplateId!.Value,
            formModel.FolderPath,
            formModel.Name
        );

        await DialogRef.CloseAsync(DialogResult.Ok(releaseId));
        navigationManager.NavigateTo($"/releases/{releaseId}");
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

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            messageStore.Add(() => formModel.Name, L["NameIsRequired"]);
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
