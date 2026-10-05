using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared.ProgressSteps;

public partial class ProgressStepBar<TKind> : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ProgressStepDisplay<TKind>> Steps { get; set; } = [];

    [Parameter]
    [EditorRequired]
    public string AriaLabel { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<TKind> OnStepSelected { get; set; }

    private static string GetMarkerClass(ProgressStepState state) =>
        state switch
        {
            ProgressStepState.Done => "bearcat-release-progress-step-marker-done",
            ProgressStepState.InProgress => "bearcat-release-progress-step-marker-in-progress",
            ProgressStepState.Attention => "bearcat-release-progress-step-marker-attention",
            ProgressStepState.Pending => "bearcat-release-progress-step-marker-pending",
            ProgressStepState.NotApplicable =>
                "bearcat-release-progress-step-marker-not-applicable",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
}
