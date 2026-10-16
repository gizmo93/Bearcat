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
    public IReadOnlySet<FirstStartSetupStep> CompletedSteps { get; set; } = null!;

    [Parameter]
    public EventCallback<FirstStartSetupStep> OnStepSelected { get; set; }

    private List<ProgressStepDisplay<FirstStartSetupStep>> GetStepDisplays()
    {
        return Enum.GetValues<FirstStartSetupStep>().Select(CreateStepDisplay).ToList();
    }

    private ProgressStepDisplay<FirstStartSetupStep> CreateStepDisplay(FirstStartSetupStep step)
    {
        var state = GetState(step);
        var (title, shortTitle, iconName) = step switch
        {
            FirstStartSetupStep.Welcome => (
                L["FirstStartSetupStepWelcome"],
                L["FirstStartSetupStepWelcome"],
                "play"
            ),
            FirstStartSetupStep.WorkingDirectories => (
                L["WorkingDirectories"],
                L["FirstStartSetupStepFoldersShort"],
                "folder-tree"
            ),
            FirstStartSetupStep.Hoster => (L["Hoster"], L["Hoster"], "server"),
            FirstStartSetupStep.ReleaseTemplate => (
                L["ReleaseTemplate"],
                L["FirstStartSetupStepTemplateShort"],
                "layout-template"
            ),
            FirstStartSetupStep.Source => (
                L["FirstStartSetupStepSource"],
                L["FirstStartSetupStepSource"],
                "folder-input"
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };
        var description = state switch
        {
            ProgressStepState.Done => L["FirstStartSetupStepDone"],
            ProgressStepState.Current => L["FirstStartSetupStepCurrent"],
            _ => L["FirstStartSetupStepOpen"],
        };

        return new ProgressStepDisplay<FirstStartSetupStep>(
            step,
            state,
            title,
            shortTitle,
            description,
            iconName
        );
    }

    private ProgressStepState GetState(FirstStartSetupStep step)
    {
        if (step == CurrentStep)
        {
            return ProgressStepState.Current;
        }

        return CompletedSteps.Contains(step) ? ProgressStepState.Done : ProgressStepState.Pending;
    }
}
