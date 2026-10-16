using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupSourceCreatedSummary(
    NavigationManager navigationManager,
    FirstStartSetupBrowserState browserState
) : ComponentBase
{
    [Parameter]
    public int? TestReleaseId { get; set; }

    [Parameter]
    public InitialUploadCreationForReleaseResult? TestReleaseUploadCreationResult { get; set; }

    [Parameter]
    public EventCallback OnBack { get; set; }

    private async Task GoToActivityAsync()
    {
        await browserState.DisableSetupAutoOpenAsync();
        navigationManager.NavigateTo("/");
    }
}
