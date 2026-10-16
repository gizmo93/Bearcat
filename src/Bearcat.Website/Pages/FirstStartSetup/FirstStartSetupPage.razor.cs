using Bearcat.Domain.UseCases.FirstStartSetup.Repositories;
using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupPage(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    FirstStartSetupBrowserState browserState
)
{
    private readonly FirstStartSetupWizardState wizardState = new();
    private List<WorkingDirectoryStatus> workingDirectoryStatuses = [];
    private FirstStartSetupDatabaseStatus databaseStatus = null!;
    private FirstStartSetupStep? currentStep;

    protected override async Task OnInitializedAsync()
    {
        LoadWorkingDirectoryStatuses();
        await LoadDatabaseStatusAsync();
        var releaseTemplates = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) => repository.GetAllAsync()
        );
        wizardState.ReleaseTemplateId = releaseTemplates.FirstOrDefault()?.ReleaseTemplateId;
        currentStep = GetStartStep();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !browserState.IsSetupAutoOpenDisabled)
        {
            await browserState.DisableSetupAutoOpenAsync();
        }
    }

    private void LoadWorkingDirectoryStatuses()
    {
        workingDirectoryStatuses = operationRunner.Run(
            (FirstStartWorkingDirectoryStatusService service) =>
                service.GetWorkingDirectoryStatuses()
        );
    }

    private async Task LoadDatabaseStatusAsync()
    {
        databaseStatus = await operationRunner.RunAsync(
            (IFirstStartSetupStatusRepository repository) => repository.GetDatabaseStatusAsync()
        );
    }

    private HashSet<FirstStartSetupStep> GetCompletedSteps()
    {
        HashSet<FirstStartSetupStep> completedSteps = [];

        if (!databaseStatus.IsFreshInstallation)
        {
            completedSteps.Add(FirstStartSetupStep.Welcome);
        }

        if (HasExistingWritableWorkingDirectory())
        {
            completedSteps.Add(FirstStartSetupStep.WorkingDirectories);
        }

        if (databaseStatus.HasHosterRegistration)
        {
            completedSteps.Add(FirstStartSetupStep.Hoster);
        }

        if (databaseStatus.HasReleaseTemplate)
        {
            completedSteps.Add(FirstStartSetupStep.ReleaseTemplate);
        }

        if (databaseStatus.HasReleaseFolderAutomationOrRemoteSourceAutomation)
        {
            completedSteps.Add(FirstStartSetupStep.Source);
        }

        return completedSteps;
    }

    private FirstStartSetupStep GetStartStep()
    {
        if (databaseStatus.IsFreshInstallation)
        {
            return FirstStartSetupStep.Welcome;
        }

        if (!HasExistingWritableWorkingDirectory())
        {
            return FirstStartSetupStep.WorkingDirectories;
        }

        if (!databaseStatus.HasHosterRegistration)
        {
            return FirstStartSetupStep.Hoster;
        }

        if (!databaseStatus.HasReleaseTemplate)
        {
            return FirstStartSetupStep.ReleaseTemplate;
        }

        return FirstStartSetupStep.Source;
    }

    private bool HasExistingWritableWorkingDirectory()
    {
        return workingDirectoryStatuses.Any(status => status is { Exists: true, IsWritable: true });
    }

    private void GoToStep(FirstStartSetupStep step)
    {
        currentStep = step;
    }

    private void GoToNextStep()
    {
        currentStep += 1;
    }

    private async Task GoToNextStepAfterChangeAsync()
    {
        await LoadDatabaseStatusAsync();
        GoToNextStep();
    }

    private void GoToPreviousStep()
    {
        currentStep -= 1;
    }

    private async Task SkipSetupAsync()
    {
        await browserState.DisableSetupAutoOpenAsync();
        navigationManager.NavigateTo("/");
    }
}
