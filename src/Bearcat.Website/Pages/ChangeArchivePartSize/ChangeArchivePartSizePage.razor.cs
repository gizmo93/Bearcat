using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.Dto;
using Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;

namespace Bearcat.Website.Pages.ChangeArchivePartSize;

public partial class ChangeArchivePartSizePage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<ArchivePartSizeChoiceReadModel>? partSizeChoices;
    private int? currentArchiveFileSizeMb;
    private int newArchiveFileSizeMb;
    private bool isNewArchiveFileSizeEntered;
    private bool includeArchiveConfigTemplates = true;
    private ArchivePartSizeChangePreview? preview;
    private int latestPreviewRequestNumber;
    private bool isLoadingPreview;
    private bool isApplying;

    private IReadOnlyList<SelectOption<int?>> PartSizeChoiceOptions
    {
        get
        {
            return partSizeChoices!
                .Select(choice => new SelectOption<int?>(
                    choice.ArchiveFileSizeMb,
                    L[
                        "ChangeArchivePartSizeChoice",
                        choice.ArchiveFileSizeMb,
                        choice.ArchiveConfigCount,
                        choice.ArchiveConfigTemplateCount
                    ]
                ))
                .ToList();
        }
    }

    private string? NewArchiveFileSizeErrorText
    {
        get
        {
            if (currentArchiveFileSizeMb is null || !isNewArchiveFileSizeEntered)
            {
                return null;
            }

            if (newArchiveFileSizeMb < 1)
            {
                return L["ChangeArchivePartSizeNewMustBePositive"];
            }

            if (newArchiveFileSizeMb == currentArchiveFileSizeMb)
            {
                return L["ChangeArchivePartSizeNewMustDiffer"];
            }

            return null;
        }
    }

    private bool HasValidInputs =>
        currentArchiveFileSizeMb is not null
        && newArchiveFileSizeMb >= 1
        && newArchiveFileSizeMb != currentArchiveFileSizeMb;

    private bool CanApply =>
        preview is not null
        && !isLoadingPreview
        && !isApplying
        && preview.ArchiveConfigCountToChange + preview.ArchiveConfigTemplateCountToChange > 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadPartSizeChoicesAsync();
    }

    private async Task LoadPartSizeChoicesAsync()
    {
        partSizeChoices = await operationRunner.RunAsync(
            (ArchivePartSizeChangeService service) =>
                service.GetPartSizeChoicesAsync(CancellationToken.None)
        );

        if (partSizeChoices.All(choice => choice.ArchiveFileSizeMb != currentArchiveFileSizeMb))
        {
            currentArchiveFileSizeMb = null;
        }
    }

    private async Task SetCurrentArchiveFileSizeAsync(int? archiveFileSizeMb)
    {
        currentArchiveFileSizeMb = archiveFileSizeMb;

        await ReloadPreviewAsync();
    }

    private async Task SetNewArchiveFileSizeAsync(int archiveFileSizeMb)
    {
        newArchiveFileSizeMb = archiveFileSizeMb;
        isNewArchiveFileSizeEntered = true;

        await ReloadPreviewAsync();
    }

    private async Task SetIncludeArchiveConfigTemplatesAsync(bool include)
    {
        includeArchiveConfigTemplates = include;

        await ReloadPreviewAsync();
    }

    private async Task ReloadPreviewAsync()
    {
        var previewRequestNumber = ++latestPreviewRequestNumber;

        if (!HasValidInputs)
        {
            preview = null;
            isLoadingPreview = false;
            return;
        }

        isLoadingPreview = true;

        var request = CreateRequest();
        var loadedPreview = await operationRunner.RunAsync(
            (ArchivePartSizeChangeService service) =>
                service.GetPreviewAsync(request, CancellationToken.None)
        );

        if (previewRequestNumber != latestPreviewRequestNumber)
        {
            return;
        }

        preview = loadedPreview;
        isLoadingPreview = false;
    }

    private ArchivePartSizeChangeRequest CreateRequest()
    {
        return new ArchivePartSizeChangeRequest(
            CurrentArchiveFileSizeMb: currentArchiveFileSizeMb!.Value,
            NewArchiveFileSizeMb: newArchiveFileSizeMb,
            IncludeArchiveConfigTemplates: includeArchiveConfigTemplates
        );
    }

    private async Task ApplyAsync()
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["ChangeArchivePartSize"],
            L[
                "ChangeArchivePartSizeConfirmation",
                preview!.ArchiveConfigCountToChange,
                preview.ArchiveConfigTemplateCountToChange,
                currentArchiveFileSizeMb!.Value,
                newArchiveFileSizeMb
            ],
            new ConfirmDialogOptions
            {
                ConfirmText = L["ChangeArchivePartSize"],
                CancelText = L["Cancel"],
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        isApplying = true;

        try
        {
            var request = CreateRequest();
            var result = await operationRunner.RunAsync(
                (ArchivePartSizeChangeService service) =>
                    service.ApplyAsync(request, CancellationToken.None)
            );

            toastService.Success(
                L[
                    "ChangeArchivePartSizeApplied",
                    result.ChangedArchiveConfigCount,
                    result.ChangedArchiveConfigTemplateCount,
                    result.SkippedArchiveConfigCount,
                    result.SkippedArchiveConfigTemplateCount
                ]
            );

            await LoadPartSizeChoicesAsync();
            await ReloadPreviewAsync();
        }
        finally
        {
            isApplying = false;
        }
    }
}
