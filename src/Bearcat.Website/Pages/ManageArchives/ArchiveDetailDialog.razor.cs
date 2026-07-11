using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageArchives;

public partial class ArchiveDetailDialog(
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

    private async Task CloseAsync()
    {
        await DialogRef.CancelAsync();
    }
}
