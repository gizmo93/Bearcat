using Bearcat.Domain.UseCases.ManageConfirmedFolders;
using Bearcat.Domain.UseCases.ManageConfirmedFolders.ReadModels;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageConfirmedFolders;

public partial class ConfirmFolderDialog(
    IScopedOperationRunner operationRunner,
    ToastService toastService
) : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public ConfirmableFolderReadModel ConfirmableFolder { get; set; } = null!;

    private FolderTopLevelEntriesReadModel? topLevelEntries;
    private bool isConfirming;

    protected override void OnInitialized()
    {
        if (!ConfirmableFolder.Exists)
        {
            return;
        }

        topLevelEntries = operationRunner.Run(
            (ConfirmedFolderService service) => service.GetTopLevelEntries(ConfirmableFolder.Path)
        );
    }

    private async Task ConfirmAsync()
    {
        isConfirming = true;

        try
        {
            await operationRunner.RunAsync(
                (ConfirmedFolderService service, CancellationToken cancellationToken) =>
                    service.ConfirmFolderAsync(ConfirmableFolder.Path, cancellationToken)
            );
        }
        catch (InvalidOperationException exception)
        {
            toastService.Error(exception.Message);
            return;
        }
        finally
        {
            isConfirming = false;
        }

        await DialogRef.CloseAsync(DialogResult.Ok(ConfirmableFolder.Path));
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
