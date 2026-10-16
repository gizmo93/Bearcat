using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Bearcat.Website.Shared.ProgressSteps;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupProgress : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public FirstStartSetupStep CurrentStep { get; set; }

    [Parameter]
    [EditorRequired]
    public FirstStartSetupStatus Status { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public FirstStartSetupWizardState WizardState { get; set; } = null!;

    [Parameter]
    public EventCallback<FirstStartSetupStep> OnStepSelected { get; set; }

    private List<ProgressStepDisplay<FirstStartSetupStep>> GetStepDisplays()
    {
        return Enum.GetValues<FirstStartSetupStep>().Select(CreateStepDisplay).ToList();
    }

    private ProgressStepDisplay<FirstStartSetupStep> CreateStepDisplay(FirstStartSetupStep step)
    {
        var state = GetState(step);

        return new ProgressStepDisplay<FirstStartSetupStep>(
            Kind: step,
            State: state,
            Title: GetTitle(step),
            ShortTitle: GetShortTitle(step),
            Description: GetDescription(state),
            IconName: GetIconName(step)
        );
    }

    private ProgressStepState GetState(FirstStartSetupStep step)
    {
        if (step == CurrentStep)
        {
            return ProgressStepState.Current;
        }

        return IsStepCompleted(step) ? ProgressStepState.Done : ProgressStepState.Pending;
    }

    private bool IsStepCompleted(FirstStartSetupStep step)
    {
        return step switch
        {
            FirstStartSetupStep.Welcome => CurrentStep > FirstStartSetupStep.Welcome,
            FirstStartSetupStep.WorkingDirectories => Status.HasExistingWritableWorkingDirectory(),
            FirstStartSetupStep.Hoster => Status.DatabaseStatus.HasHosterRegistration
                || WizardState.HasSelectedHosterRegistrationWithSuccessfulLogin(),
            FirstStartSetupStep.ReleaseTemplate => Status.DatabaseStatus.HasReleaseTemplate
                || WizardState.ReleaseTemplateId is not null,
            FirstStartSetupStep.Source => Status
                .DatabaseStatus
                .HasReleaseFolderAutomationOrRemoteSourceAutomation,
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };
    }

    private string GetTitle(FirstStartSetupStep step)
    {
        return step switch
        {
            FirstStartSetupStep.Welcome => L["FirstStartSetupStepWelcome"],
            FirstStartSetupStep.WorkingDirectories => L["WorkingDirectories"],
            FirstStartSetupStep.Hoster => L["Hoster"],
            FirstStartSetupStep.ReleaseTemplate => L["ReleaseTemplate"],
            FirstStartSetupStep.Source => L["FirstStartSetupStepSource"],
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };
    }

    private string GetShortTitle(FirstStartSetupStep step)
    {
        return step switch
        {
            FirstStartSetupStep.WorkingDirectories => L["FirstStartSetupStepFoldersShort"],
            FirstStartSetupStep.ReleaseTemplate => L["FirstStartSetupStepTemplateShort"],
            _ => GetTitle(step),
        };
    }

    private string GetDescription(ProgressStepState state)
    {
        return state switch
        {
            ProgressStepState.Current => L["FirstStartSetupStepCurrent"],
            ProgressStepState.Done => L["FirstStartSetupStepDone"],
            _ => L["FirstStartSetupStepOpen"],
        };
    }

    private static string GetIconName(FirstStartSetupStep step)
    {
        return step switch
        {
            FirstStartSetupStep.Welcome => "play",
            FirstStartSetupStep.WorkingDirectories => "folder-tree",
            FirstStartSetupStep.Hoster => "server",
            FirstStartSetupStep.ReleaseTemplate => "layout-template",
            FirstStartSetupStep.Source => "folder-input",
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };
    }
}
