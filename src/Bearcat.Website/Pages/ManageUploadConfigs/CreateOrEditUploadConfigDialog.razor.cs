using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared.UploadTargets;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageUploadConfigs;

public partial class CreateOrEditUploadConfigDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? UploadConfigId { get; set; }

    [Parameter]
    public bool AllowsVideoFileUploads { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEdit => UploadConfigId.HasValue;
    private UploadConfigFormModel formModel = null!;
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private UploadTargetChoices uploadTargetChoices = null!;
    private IReadOnlyList<ArchiveConfigOptionReadModel> archiveConfigOptions = [];
    private bool isInitialized;

    private bool ShowUploadSource =>
        AllowsVideoFileUploads || formModel.UploadSource is UploadSource.VideoFiles;

    private bool UsesArchiveFiles => formModel.UploadSource is UploadSource.ArchiveFiles;

    private bool CanUsePremiumOnlyDownload =>
        uploadTargetChoices.SupportsPremiumOnlyDownloads(formModel.UploadTarget);

    private ArchiveConfigOptionReadModel? SelectedArchiveConfig =>
        formModel.ArchiveConfigId is null
            ? null
            : archiveConfigOptions.FirstOrDefault(config =>
                config.ArchiveConfigId == formModel.ArchiveConfigId
            );

    protected override async Task OnInitializedAsync()
    {
        await InitializeFormModelAsync();
        uploadTargetChoices = await UploadTargetChoices.LoadAsync(operationRunner);
        archiveConfigOptions = await operationRunner.RunAsync(
            (IUploadConfigReadRepository repository) =>
                repository.GetArchiveConfigOptionsAsync(ReleaseId)
        );
        ResetPremiumOnlyDownloadIfUnsupported();

        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        await operationRunner.RunAsync<UploadConfigService>(async service =>
        {
            if (IsEdit)
            {
                await service.UpdateAsync(
                    uploadConfigId: UploadConfigId!.Value,
                    name: formModel.Name!,
                    uploadTarget: formModel.UploadTarget!,
                    archiveConfigId: UsesArchiveFiles ? formModel.ArchiveConfigId : null,
                    premiumOnlyDownload: CanUsePremiumOnlyDownload && formModel.PremiumOnlyDownload
                );
                return;
            }

            await service.CreateAsync(
                releaseId: ReleaseId,
                name: formModel.Name!,
                uploadTarget: formModel.UploadTarget!,
                uploadSource: formModel.UploadSource,
                archiveConfigId: UsesArchiveFiles ? formModel.ArchiveConfigId : null,
                premiumOnlyDownload: CanUsePremiumOnlyDownload && formModel.PremiumOnlyDownload
            );
        });

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            messageStore.Add(() => formModel.Name!, L["NameIsRequired"]);
        }

        if (formModel.UploadTarget is null)
        {
            messageStore.Add(() => formModel.UploadTarget!, L["UploadTargetRequired"]);
        }

        if (UsesArchiveFiles && formModel.ArchiveConfigId is null)
        {
            messageStore.Add(() => formModel.ArchiveConfigId!, L["ArchiveConfigRequired"]);
        }

        if (
            UsesArchiveFiles
            && uploadTargetChoices.GetMaxFileSizeMb(formModel.UploadTarget) is { } maxFileSizeMb
            && SelectedArchiveConfig is { } archiveConfig
            && archiveConfig.ArchiveFileSizeMb > maxFileSizeMb
        )
        {
            messageStore.Add(
                () => formModel.ArchiveConfigId!,
                L[
                    "ArchiveFileSizeExceedsHosterLimit",
                    archiveConfig.ArchiveFileSizeMb,
                    maxFileSizeMb
                ]
            );
        }
    }

    private void OnUploadSourceChanged()
    {
        if (
            formModel.UploadTarget is { } uploadTarget
            && !uploadTargetChoices.MatchesUploadSource(uploadTarget, formModel.UploadSource)
        )
        {
            formModel.UploadTarget = null;
        }

        ResetPremiumOnlyDownloadIfUnsupported();
    }

    private void OnUploadTargetChanged()
    {
        ResetPremiumOnlyDownloadIfUnsupported();
    }

    private void ResetPremiumOnlyDownloadIfUnsupported()
    {
        if (!CanUsePremiumOnlyDownload)
        {
            formModel.PremiumOnlyDownload = false;
        }
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEdit)
        {
            formModel = new UploadConfigFormModel();
            return;
        }

        var uploadConfig = await operationRunner.RunAsync(
            (IUploadConfigReadRepository repository) =>
                repository.GetReadModelByIdAsync(UploadConfigId!.Value)
        );

        formModel = new UploadConfigFormModel
        {
            Name = uploadConfig.Name,
            UploadSource = uploadConfig.UploadSource,
            UploadTarget = UploadTarget.FromIds(
                uploadConfig.HosterRegistrationId,
                uploadConfig.HosterAccountPoolId
            ),
            ArchiveConfigId = uploadConfig.ArchiveConfigId,
            PremiumOnlyDownload = uploadConfig.PremiumOnlyDownload,
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
