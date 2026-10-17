using Bearcat.Domain.UseCases.ManageArchives.FailedArchiveCreations;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageArchives;

public partial class ArchiveDetailDialog(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public int ArchiveId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private ArchiveReadModel Archive { get; set; } = null!;

    private bool isInitialized;

    private bool CanDeleteArchive => Archive.ArchiveState is ArchiveState.CreationFailed;

    protected override async Task OnInitializedAsync()
    {
        var archive = await operationRunner.RunAsync(
            (IArchiveReadRepository repository) => repository.GetByIdAsync(ArchiveId)
        );

        if (archive is null)
        {
            toastService.Error(L["ArchiveNotFound", ArchiveId]);
            await DialogRef.CancelAsync();
            return;
        }

        Archive = archive;
        isInitialized = true;
    }

    private async Task DeleteCreationFailedArchiveAsync()
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["DeleteCreationFailedArchive"],
            L["DeleteCreationFailedArchiveConfirmation", Archive.ArchiveFolderPath],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        await operationRunner.RunAsync(
            (FailedArchiveCreationService service) =>
                service.DeleteCreationFailedArchiveAsync(ArchiveId)
        );

        toastService.Success(L["CreationFailedArchiveDeleted"]);
        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task CloseAsync()
    {
        await DialogRef.CancelAsync();
    }
}
