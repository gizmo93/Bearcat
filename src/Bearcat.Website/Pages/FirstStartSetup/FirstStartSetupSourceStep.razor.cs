using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupSourceStep(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public FirstStartSetupWizardState WizardState { get; set; } = null!;

    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback OnSourceCreated { get; set; }

    private IReadOnlyList<ReleaseTemplateSummaryReadModel> releaseTemplates = [];
    private List<SelectOption<int?>> releaseTemplateOptions = [];
    private FirstStartSetupSourceKind sourceKind = FirstStartSetupSourceKind.LocalFolder;
    private FirstStartSetupCreatedSource? createdSource;
    private int existingSourceCount;
    private bool showsReleaseTemplateSelection;
    private bool isLoading = true;

    private ReleaseTemplateSummaryReadModel? SelectedReleaseTemplate =>
        releaseTemplates.FirstOrDefault(releaseTemplate =>
            releaseTemplate.ReleaseTemplateId == WizardState.ReleaseTemplateId
        );

    protected override async Task OnInitializedAsync()
    {
        releaseTemplates = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) => repository.GetAllAsync()
        );
        releaseTemplateOptions = releaseTemplates
            .Select(releaseTemplate => new SelectOption<int?>(
                releaseTemplate.ReleaseTemplateId,
                releaseTemplate.Name
            ))
            .ToList();
        var releaseFolderAutomations = await operationRunner.RunAsync(
            (IReleaseFolderAutomationReadRepository repository) => repository.GetAllAsync()
        );
        var remoteSourceAutomations = await operationRunner.RunAsync(
            (IRemoteSourceAutomationReadRepository repository) => repository.GetAllAsync()
        );
        existingSourceCount = releaseFolderAutomations.Count + remoteSourceAutomations.Count;

        if (SelectedReleaseTemplate is null)
        {
            showsReleaseTemplateSelection = true;
            WizardState.ReleaseTemplateId = releaseTemplates.FirstOrDefault()?.ReleaseTemplateId;
        }

        isLoading = false;
    }

    private void SelectReleaseTemplate(int? releaseTemplateId)
    {
        WizardState.ReleaseTemplateId = releaseTemplateId;
    }

    private string GetChoiceClass(FirstStartSetupSourceKind sourceKindToRender)
    {
        return FirstStartSetupChoiceCardStyle.GetClass(sourceKind == sourceKindToRender);
    }

    private string IsCheckedAttributeValue(FirstStartSetupSourceKind sourceKindToRender)
    {
        return sourceKind == sourceKindToRender ? "true" : "false";
    }

    private void SelectSourceKind(FirstStartSetupSourceKind selectedSourceKind)
    {
        sourceKind = selectedSourceKind;
    }

    private async Task HandleSourceCreatedAsync(FirstStartSetupCreatedSource source)
    {
        createdSource = source;
        await OnSourceCreated.InvokeAsync();
    }
}
