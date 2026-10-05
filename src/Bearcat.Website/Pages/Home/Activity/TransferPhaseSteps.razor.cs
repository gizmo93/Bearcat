using Bearcat.Domain.Shared.Transfers;
using Bearcat.Website.Shared.ProgressSteps;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Activity;

public partial class TransferPhaseSteps : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TransferPhase> Phases { get; set; } = [];

    [Parameter]
    [EditorRequired]
    public TransferType CurrentPhase { get; set; }

    [Parameter]
    [EditorRequired]
    public string AriaLabel { get; set; } = null!;

    private int GetCurrentPhaseIndex()
    {
        var index = Phases.ToList().FindIndex(phase => phase.Type == CurrentPhase);

        return index >= 0
            ? index
            : throw new ArgumentOutOfRangeException(
                nameof(CurrentPhase),
                CurrentPhase,
                "Current phase is not part of the phases"
            );
    }

    private static ProgressStepState GetState(int index, int currentPhaseIndex)
    {
        if (index < currentPhaseIndex)
        {
            return ProgressStepState.Done;
        }

        return index == currentPhaseIndex
            ? ProgressStepState.InProgress
            : ProgressStepState.Pending;
    }

    private static string GetStepClass(ProgressStepState state) =>
        state switch
        {
            ProgressStepState.Done => "bearcat-transfer-phase-step-done",
            ProgressStepState.InProgress => "bearcat-transfer-phase-step-in-progress",
            ProgressStepState.Pending => "bearcat-transfer-phase-step-pending",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
}
