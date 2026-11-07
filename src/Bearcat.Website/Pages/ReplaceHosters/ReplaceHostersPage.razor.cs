using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters;
using Bearcat.Domain.UseCases.ReplaceHosters.Dto;
using Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared.UploadTargets;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ReplaceHosters;

public partial class ReplaceHostersPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private const string SourceChoiceGridClass = "lg:grid-cols-[minmax(0,1fr)_12rem_16rem]";

    private readonly HashSet<UploadTarget> selectedSourceUploadTargets = [];
    private UploadTargetChoices? uploadTargetChoices;
    private IReadOnlyList<UploadTargetHoster> hosters = [];
    private IReadOnlyList<HosterReplacementSourceChoiceReadModel> sourceChoices = [];
    private string? sourceHosterClassName;
    private UploadTarget? replacementUploadTarget;
    private bool markSourceHosterAsDead;
    private HosterReplacementPreview? preview;
    private int latestPreviewRequestNumber;
    private bool isLoadingPreview;
    private bool isExecuting;

    private UploadSource ReplacementUploadSource =>
        hosters.First(hoster => hoster.HosterClassName == sourceHosterClassName).IsVideoHoster
            ? UploadSource.VideoFiles
            : UploadSource.ArchiveFiles;

    private bool CanMarkSourceHosterAsDead =>
        replacementUploadTarget is not null
        && uploadTargetChoices!.GetHosterClassName(replacementUploadTarget)
            != sourceHosterClassName;

    private bool HasCompleteInputs =>
        replacementUploadTarget is not null && selectedSourceUploadTargets.Count > 0;

    private bool CanExecute =>
        preview is not null && !isLoadingPreview && !isExecuting && WouldChangeAnything(preview);

    protected override async Task OnInitializedAsync()
    {
        await LoadUploadTargetChoicesAsync();
    }

    private async Task LoadUploadTargetChoicesAsync()
    {
        uploadTargetChoices = await UploadTargetChoices.LoadAsync(operationRunner);
        hosters = uploadTargetChoices.GetHosters();
    }

    private async Task OnSourceHosterChangedAsync(string? hosterClassName)
    {
        sourceHosterClassName = hosterClassName;
        sourceChoices = [];
        selectedSourceUploadTargets.Clear();
        replacementUploadTarget = null;
        markSourceHosterAsDead = false;
        preview = null;
        isLoadingPreview = false;
        latestPreviewRequestNumber++;

        var loadedSourceChoices = await LoadSourceChoicesAsync();

        if (hosterClassName != sourceHosterClassName)
        {
            return;
        }

        sourceChoices = loadedSourceChoices;
        selectedSourceUploadTargets.UnionWith(
            loadedSourceChoices.Select(sourceChoice => sourceChoice.UploadTarget)
        );
    }

    private async Task<
        IReadOnlyList<HosterReplacementSourceChoiceReadModel>
    > LoadSourceChoicesAsync()
    {
        var hosterClassName = sourceHosterClassName!;

        return await operationRunner.RunAsync(
            (HosterReplacementService service) => service.GetSourceChoicesAsync(hosterClassName)
        );
    }

    private async Task SetSourceSelectedAsync(UploadTarget uploadTarget, bool isSelected)
    {
        if (isSelected)
        {
            selectedSourceUploadTargets.Add(uploadTarget);
        }
        else
        {
            selectedSourceUploadTargets.Remove(uploadTarget);
        }

        if (isSelected && uploadTarget == replacementUploadTarget)
        {
            replacementUploadTarget = null;
        }

        await ReloadPreviewAsync();
    }

    private bool CanBeReplacement(UploadTarget uploadTarget)
    {
        return !selectedSourceUploadTargets.Contains(uploadTarget)
            && uploadTargetChoices!.IsUsableForUploads(uploadTarget);
    }

    private async Task OnReplacementUploadTargetChangedAsync()
    {
        markSourceHosterAsDead = markSourceHosterAsDead && CanMarkSourceHosterAsDead;

        await ReloadPreviewAsync();
    }

    private async Task SetMarkSourceHosterAsDeadAsync(bool isMarkedAsDead)
    {
        markSourceHosterAsDead = isMarkedAsDead;

        await ReloadPreviewAsync();
    }

    private async Task ReloadPreviewAsync()
    {
        var previewRequestNumber = ++latestPreviewRequestNumber;

        if (!HasCompleteInputs)
        {
            preview = null;
            isLoadingPreview = false;
            return;
        }

        isLoadingPreview = true;

        var request = CreateRequest();
        var loadedPreview = await operationRunner.RunAsync(
            (HosterReplacementService service) => service.GetPreviewAsync(request)
        );

        if (previewRequestNumber != latestPreviewRequestNumber)
        {
            return;
        }

        preview = loadedPreview;
        isLoadingPreview = false;
    }

    private HosterReplacementRequest CreateRequest()
    {
        return new HosterReplacementRequest(
            SourceUploadTargets: selectedSourceUploadTargets.ToList(),
            ReplacementUploadTarget: replacementUploadTarget!,
            MarkSourceHosterAsDead: markSourceHosterAsDead
        );
    }

    private static bool WouldChangeAnything(HosterReplacementPreview hosterReplacementPreview)
    {
        return hosterReplacementPreview.UploadConfigCount > 0
            || hosterReplacementPreview.UploadConfigTemplateCount > 0
            || hosterReplacementPreview.UnstartedUploadCount > 0
            || hosterReplacementPreview.AccountCountToDeactivate > 0
            || hosterReplacementPreview.UploadCountToMarkOffline > 0;
    }

    private async Task ExecuteAsync()
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["ReplaceHosters"],
            CreateConfirmationMessage(preview!),
            new ConfirmDialogOptions
            {
                ConfirmText = L["ReplaceHostersExecute"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        isExecuting = true;

        try
        {
            var request = CreateRequest();
            var result = await operationRunner.RunAsync(
                (HosterReplacementService service) => service.ExecuteAsync(request)
            );

            toastService.Success(CreateSuccessMessage(result));

            await LoadUploadTargetChoicesAsync();
            sourceChoices = await LoadSourceChoicesAsync();
            await ReloadPreviewAsync();
        }
        finally
        {
            isExecuting = false;
        }
    }

    private string CreateConfirmationMessage(HosterReplacementPreview hosterReplacementPreview)
    {
        var replacementName = uploadTargetChoices!.GetName(replacementUploadTarget!);

        return markSourceHosterAsDead
            ? L[
                "ReplaceHostersConfirmationWithDeadHoster",
                hosterReplacementPreview.UploadConfigCount,
                hosterReplacementPreview.UploadConfigTemplateCount,
                hosterReplacementPreview.UnstartedUploadCount,
                replacementName,
                hosterReplacementPreview.AccountCountToDeactivate,
                hosterReplacementPreview.UploadCountToMarkOffline
            ]
            : L[
                "ReplaceHostersConfirmation",
                hosterReplacementPreview.UploadConfigCount,
                hosterReplacementPreview.UploadConfigTemplateCount,
                hosterReplacementPreview.UnstartedUploadCount,
                replacementName
            ];
    }

    private string CreateSuccessMessage(HosterReplacementPreview result)
    {
        return markSourceHosterAsDead
            ? L[
                "ReplaceHostersExecutedWithDeadHoster",
                result.UploadConfigCount,
                result.UploadConfigTemplateCount,
                result.UnstartedUploadCount,
                result.AccountCountToDeactivate,
                result.UploadCountToMarkOffline
            ]
            : L[
                "ReplaceHostersExecuted",
                result.UploadConfigCount,
                result.UploadConfigTemplateCount,
                result.UnstartedUploadCount
            ];
    }
}
