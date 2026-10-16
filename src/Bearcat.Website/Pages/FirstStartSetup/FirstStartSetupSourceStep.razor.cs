using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.Repositories;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupSourceStep(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    public int? ReleaseTemplateId { get; set; }

    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback OnSourceCreated { get; set; }

    private List<string> existingSourceNames = [];
    private bool usesRemoteSource;
    private bool isSourceCreated;

    protected override async Task OnInitializedAsync()
    {
        await LoadExistingSourceNamesAsync();
    }

    private async Task LoadExistingSourceNamesAsync()
    {
        var releaseFolderAutomations = await operationRunner.RunAsync(
            (IReleaseFolderAutomationReadRepository repository) => repository.GetAllAsync()
        );
        var remoteSourceAutomations = await operationRunner.RunAsync(
            (IRemoteSourceAutomationReadRepository repository) => repository.GetAllAsync()
        );

        existingSourceNames =
        [
            .. releaseFolderAutomations.Select(automation => automation.BasePath),
            .. remoteSourceAutomations.Select(automation => automation.Name),
        ];
    }

    private async Task HandleSourceCreatedAsync()
    {
        isSourceCreated = true;
        await LoadExistingSourceNamesAsync();
        await OnSourceCreated.InvokeAsync();
    }
}
