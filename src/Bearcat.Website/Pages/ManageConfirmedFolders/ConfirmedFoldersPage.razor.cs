using Bearcat.Domain.Shared.FolderConfirmation;
using Bearcat.Domain.UseCases.ManageConfirmedFolders;
using Bearcat.Domain.UseCases.ManageConfirmedFolders.ReadModels;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;

namespace Bearcat.Website.Pages.ManageConfirmedFolders;

public partial class ConfirmedFoldersPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<ConfirmableFolderReadModel> confirmableFolders = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadConfirmableFoldersAsync();
    }

    private async Task LoadConfirmableFoldersAsync()
    {
        confirmableFolders = await operationRunner.RunAsync(
            (ConfirmedFolderService service, CancellationToken cancellationToken) =>
                service.GetConfirmableFoldersAsync(cancellationToken)
        );
    }

    private static BadgeVariant GetBadgeVariant(FolderConfirmationState state)
    {
        return state is FolderConfirmationState.Confirmed
            ? BadgeVariant.Secondary
            : BadgeVariant.Destructive;
    }

    private async Task ShowConfirmDialogAsync(ConfirmableFolderReadModel confirmableFolder)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ConfirmFolderDialog.ConfirmableFolder)] = confirmableFolder,
        };

        var dialog = await dialogService.OpenAsync<ConfirmFolderDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["ConfirmFolder"],
                Description = L["ConfirmFolderDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(L["FolderConfirmed", confirmableFolder.Path]);
        await LoadConfirmableFoldersAsync();
    }
}
