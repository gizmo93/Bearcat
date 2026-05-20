using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseFolderAutomations;

public partial class CreateOrEditReleaseFolderAutomationDialog(
    IReleaseTemplateReadRepository releaseTemplateReadRepository,
    DialogService dialogService,
    IConfiguration configuration
) : OwningComponentBase
{
    [Parameter]
    public ReleaseFolderAutomationFormModel FormModel { get; set; } = null!;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<ReleaseTemplateSummaryDto> releaseTemplates = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;

    private IEnumerable<SelectOption<int?>> ReleaseTemplateOptions =>
        releaseTemplates.Select(template => new SelectOption<int?>(
            template.ReleaseTemplateId,
            template.Name
        ));

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        releaseTemplates = await releaseTemplateReadRepository.GetAllAsync();
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseFolderAutomationService>();

        if (FormModel.IsEdit && FormModel.ReleaseFolderAutomationId is not null)
        {
            await service.UpdateAsync(
                FormModel.ReleaseFolderAutomationId.Value,
                FormModel.BasePath,
                FormModel.FolderNamePattern,
                FormModel.ReleaseTemplateId!.Value,
                FormModel.IsEnabled
            );
        }
        else
        {
            await service.CreateAsync(
                FormModel.BasePath,
                FormModel.FolderNamePattern,
                FormModel.ReleaseTemplateId!.Value,
                FormModel.IsEnabled
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task OpenFolderDialogAsync()
    {
        var releasesPath = configuration.GetRequiredSection("ReleaseDataDirectory").Value!;
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPath)] = releasesPath,
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
