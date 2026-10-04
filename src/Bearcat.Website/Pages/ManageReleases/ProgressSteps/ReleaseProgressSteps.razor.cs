using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.ProgressSteps;

public partial class ReleaseProgressSteps : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ReleaseProgressStep> Steps { get; set; } = [];

    [Parameter]
    public string? ActiveTab { get; set; }

    [Parameter]
    public EventCallback<ReleaseProgressStepKind> OnStepSelected { get; set; }

    private string GetTitle(ReleaseProgressStepKind kind) =>
        kind switch
        {
            ReleaseProgressStepKind.Info => L["ReleaseProgressInfo"],
            ReleaseProgressStepKind.Archived => L["ReleaseProgressArchived"],
            ReleaseProgressStepKind.Uploaded => L["ReleaseProgressUploaded"],
            ReleaseProgressStepKind.LinkContainers => L["ReleaseProgressLinkContainers"],
            ReleaseProgressStepKind.Posted => L["ReleaseProgressPosted"],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private string GetShortTitle(ReleaseProgressStepKind kind) =>
        kind is ReleaseProgressStepKind.LinkContainers ? L["Containers"] : GetTitle(kind);

    private static string GetIconName(ReleaseProgressStepKind kind) =>
        kind switch
        {
            ReleaseProgressStepKind.Info => "info",
            ReleaseProgressStepKind.Archived => "archive",
            ReleaseProgressStepKind.Uploaded => "upload",
            ReleaseProgressStepKind.LinkContainers => "link",
            ReleaseProgressStepKind.Posted => "globe",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private static string GetMarkerClass(ReleaseProgressStepState state) =>
        state switch
        {
            ReleaseProgressStepState.Done => "bearcat-release-progress-step-marker-done",
            ReleaseProgressStepState.InProgress =>
                "bearcat-release-progress-step-marker-in-progress",
            ReleaseProgressStepState.Attention => "bearcat-release-progress-step-marker-attention",
            ReleaseProgressStepState.Pending => "bearcat-release-progress-step-marker-pending",
            ReleaseProgressStepState.NotApplicable =>
                "bearcat-release-progress-step-marker-not-applicable",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };

    private string GetDescription(ReleaseProgressStep step) =>
        step.Kind switch
        {
            ReleaseProgressStepKind.Info => GetInfoDescription(step),
            ReleaseProgressStepKind.Archived => GetArchivedDescription(step),
            ReleaseProgressStepKind.Uploaded => GetUploadedDescription(step),
            ReleaseProgressStepKind.LinkContainers => GetLinkContainersDescription(step),
            ReleaseProgressStepKind.Posted => GetPostedDescription(step),
            _ => throw new ArgumentOutOfRangeException(nameof(step), step.Kind, null),
        };

    private string GetInfoDescription(ReleaseProgressStep step) =>
        step.State is ReleaseProgressStepState.Done
            ? string.Join(" · ", step.DatabaseNames)
            : L["ReleaseProgressNotResolvedYet"];

    private string GetArchivedDescription(ReleaseProgressStep step) =>
        step.State switch
        {
            ReleaseProgressStepState.Done => step.Count == 1
                ? L["ReleaseProgressArchiveCountOne"]
                : L["ReleaseProgressArchiveCount", step.Count],
            ReleaseProgressStepState.InProgress => L["ReleaseProgressArchiving"],
            ReleaseProgressStepState.Attention => L.Localize(step.ArchiveProblemState!.Value),
            _ => L["NoArchivesYet"],
        };

    private string GetUploadedDescription(ReleaseProgressStep step) =>
        step.State switch
        {
            ReleaseProgressStepState.Pending => L["ReleaseProgressNoUploadConfigs"],
            ReleaseProgressStepState.InProgress => L.Localize(UploadState.Uploading),
            _ => L["ReleaseProgressOnlineCount", step.Count, step.TotalCount],
        };

    private string GetLinkContainersDescription(ReleaseProgressStep step) =>
        step.State switch
        {
            ReleaseProgressStepState.Attention => L["ReleaseProgressContainersFailed", step.Count],
            ReleaseProgressStepState.Done => L["ReleaseProgressContainersCreated", step.Count],
            _ => L["ReleaseProgressNoContainers"],
        };

    private string GetPostedDescription(ReleaseProgressStep step) =>
        step.State switch
        {
            ReleaseProgressStepState.Pending => L["ReleaseProgressNotPostedYet"],
            _ when step.Count == 0 => L["ReleaseProgressMarkedAsPosted"],
            _ when step.Count == 1 => L["ReleaseProgressForumCountOne"],
            _ => L["ReleaseProgressForumCount", step.Count],
        };
}
