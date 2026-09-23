using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageRemoteSourceAutomations;

public partial class RemoteSourceAutomationsPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<RemoteSourceAutomationReadModel> automations = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadAutomationsAsync();
    }

    private async Task LoadAutomationsAsync()
    {
        automations = await operationRunner.RunAsync(
            (IRemoteSourceAutomationReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        await OpenDialogAsync(
            new RemoteSourceAutomationFormModel(),
            L["NewRemoteSourceAutomation"]
        );
    }

    private async Task ShowEditDialogAsync(RemoteSourceAutomationReadModel automation)
    {
        var formModel = new RemoteSourceAutomationFormModel
        {
            RemoteSourceAutomationId = automation.Id,
            Name = automation.Name,
            RemoteSourceRegistrationId = automation.RemoteSourceRegistrationId,
            RemotePath = automation.RemotePath,
            TargetPath = automation.TargetPath,
            IgnoreExistingOnFirstScan = automation.IgnoreExistingOnFirstScan,
            FolderNamePattern = automation.FolderNamePattern,
            ReleaseTemplateId = automation.ReleaseTemplateId,
            PrimaryLanguageCode = automation.PrimaryLanguageCode ?? string.Empty,
            KeepRawFiles = automation.KeepRawFiles,
            Priority = automation.Priority,
        };

        await OpenDialogAsync(formModel, L["EditNamedItem", automation.Name]);
    }

    private async Task OpenDialogAsync(RemoteSourceAutomationFormModel formModel, string title)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditRemoteSourceAutomationDialog.FormModel)] = formModel,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditRemoteSourceAutomationDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = title,
                Description = L["RemoteSourceAutomationDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadAutomationsAsync();
        }
    }

    private async Task ToggleEnabledAsync(RemoteSourceAutomationReadModel automation)
    {
        await operationRunner.RunAsync(
            (RemoteSourceAutomationService service) =>
                service.SetEnabledAsync(automation.Id, !automation.IsEnabled)
        );

        toastService.Success(
            automation.IsEnabled
                ? L["RemoteSourceAutomationDisabled", automation.Name]
                : L["RemoteSourceAutomationEnabled", automation.Name]
        );
        await LoadAutomationsAsync();
    }

    private async Task DeleteAsync(RemoteSourceAutomationReadModel automation)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", automation.Name],
            L["DeleteRemoteSourceAutomationConfirmation", automation.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await operationRunner.RunAsync(
            (RemoteSourceAutomationService service) => service.DeleteAsync(automation.Id)
        );
        await LoadAutomationsAsync();
    }

    private static BadgeVariant GetStateBadgeVariant(RemoteSourceDownloadState state)
    {
        return state switch
        {
            RemoteSourceDownloadState.Failed => BadgeVariant.Destructive,
            RemoteSourceDownloadState.Pending or RemoteSourceDownloadState.Downloading =>
                BadgeVariant.Default,
            RemoteSourceDownloadState.Ignored or RemoteSourceDownloadState.Canceled =>
                BadgeVariant.Outline,
            _ => BadgeVariant.Secondary,
        };
    }
}
