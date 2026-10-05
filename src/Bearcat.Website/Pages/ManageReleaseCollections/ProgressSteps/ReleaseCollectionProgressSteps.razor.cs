using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.Shared.ProgressSteps;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections.ProgressSteps;

public partial class ReleaseCollectionProgressSteps : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ReleaseCollectionProgressStep> Steps { get; set; } = [];

    [Parameter]
    public EventCallback<ReleaseCollectionProgressStepKind> OnStepSelected { get; set; }

    private IReadOnlyList<ProgressStepDisplay<ReleaseCollectionProgressStepKind>> StepDisplays =>
        Steps
            .Select(step => new ProgressStepDisplay<ReleaseCollectionProgressStepKind>(
                Kind: step.Kind,
                State: step.State,
                Title: GetTitle(step.Kind),
                ShortTitle: GetShortTitle(step.Kind),
                Description: GetDescription(step),
                IconName: GetIconName(step.Kind)
            ))
            .ToList();

    private string GetTitle(ReleaseCollectionProgressStepKind kind) =>
        kind switch
        {
            ReleaseCollectionProgressStepKind.Info => L["ReleaseProgressInfo"],
            ReleaseCollectionProgressStepKind.Releases => L["Releases"],
            ReleaseCollectionProgressStepKind.LinkContainers => L["ReleaseProgressLinkContainers"],
            ReleaseCollectionProgressStepKind.Images => L["Images"],
            ReleaseCollectionProgressStepKind.Posted => L["ReleaseProgressPosted"],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private string GetShortTitle(ReleaseCollectionProgressStepKind kind) =>
        kind is ReleaseCollectionProgressStepKind.LinkContainers ? L["Containers"] : GetTitle(kind);

    private static string GetIconName(ReleaseCollectionProgressStepKind kind) =>
        kind switch
        {
            ReleaseCollectionProgressStepKind.Info => "info",
            ReleaseCollectionProgressStepKind.Releases => "list-video",
            ReleaseCollectionProgressStepKind.LinkContainers => "link",
            ReleaseCollectionProgressStepKind.Images => "images",
            ReleaseCollectionProgressStepKind.Posted => "globe",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private string GetDescription(ReleaseCollectionProgressStep step) =>
        step.Kind switch
        {
            ReleaseCollectionProgressStepKind.Info => GetInfoDescription(step),
            ReleaseCollectionProgressStepKind.Releases => GetReleasesDescription(step),
            ReleaseCollectionProgressStepKind.LinkContainers => GetLinkContainersDescription(step),
            ReleaseCollectionProgressStepKind.Images => GetImagesDescription(step),
            ReleaseCollectionProgressStepKind.Posted => GetPostedDescription(step),
            _ => throw new ArgumentOutOfRangeException(nameof(step), step.Kind, null),
        };

    private string GetInfoDescription(ReleaseCollectionProgressStep step) =>
        step.State is ProgressStepState.Done
            ? step.MetadataDatabaseName!
            : L["ReleaseProgressNotResolvedYet"];

    private string GetReleasesDescription(ReleaseCollectionProgressStep step) =>
        step.State is ProgressStepState.Pending
            ? L["ReleaseProgressNoUploadConfigs"]
            : L["ReleaseProgressOnlineCount", step.Count, step.TotalCount];

    private string GetLinkContainersDescription(ReleaseCollectionProgressStep step) =>
        step.State switch
        {
            ProgressStepState.Attention => L["ReleaseProgressContainersFailed", step.Count],
            ProgressStepState.Done => L["ReleaseProgressContainersCreated", step.Count],
            ProgressStepState.Pending => L["ReleaseCollectionProgressContainersNotCreatedYet"],
            _ => L["ReleaseProgressNoContainers"],
        };

    private string GetImagesDescription(ReleaseCollectionProgressStep step) =>
        step.State switch
        {
            ProgressStepState.Attention => L["ReleaseCollectionProgressImagesFailed", step.Count],
            ProgressStepState.InProgress => L.Localize(UploadState.Uploading),
            ProgressStepState.Done => L["ReleaseCollectionProgressImagesUploaded", step.Count],
            ProgressStepState.Pending => L["ImageUploadPending"],
            _ => L["None"],
        };

    private string GetPostedDescription(ReleaseCollectionProgressStep step) =>
        step.State switch
        {
            ProgressStepState.Pending => L["ReleaseProgressNotPostedYet"],
            _ when step.Count == 0 => L["ReleaseProgressMarkedAsPosted"],
            _ when step.Count == 1 => L["ReleaseProgressForumCountOne"],
            _ => L["ReleaseProgressForumCount", step.Count],
        };
}
