using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupPage(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager
)
{
    private readonly FirstStartSetupWizardState wizardState = new();
    private FirstStartSetupStatus? status;
    private FirstStartSetupStep currentStep = FirstStartSetupStep.Welcome;

    protected override async Task OnInitializedAsync()
    {
        await LoadStatusAsync();
        currentStep = GetStartStep(status!);
    }

    private async Task LoadStatusAsync()
    {
        status = await operationRunner.RunAsync(
            (FirstStartSetupStatusService service) => service.GetStatusAsync()
        );
    }

    private static FirstStartSetupStep GetStartStep(FirstStartSetupStatus setupStatus)
    {
        if (setupStatus.IsFreshInstallation)
        {
            return FirstStartSetupStep.Welcome;
        }

        if (!setupStatus.HasExistingWritableWorkingDirectory())
        {
            return FirstStartSetupStep.WorkingDirectories;
        }

        if (!setupStatus.HasHosterRegistration)
        {
            return FirstStartSetupStep.Hoster;
        }

        if (!setupStatus.HasReleaseTemplate)
        {
            return FirstStartSetupStep.ReleaseTemplate;
        }

        return FirstStartSetupStep.Source;
    }

    private void GoToStep(FirstStartSetupStep step)
    {
        currentStep = step;
    }

    private void GoToNextStep()
    {
        currentStep += 1;
    }

    private void GoToPreviousStep()
    {
        currentStep -= 1;
    }

    private void SkipSetup()
    {
        navigationManager.NavigateTo("/");
    }
}
