using Bearcat.Domain.UseCases.ManageArchives.Dto;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Blueprint.Pages.ManageArchives;

public partial class ArchiveDetailDialog(
    IArchiveReadRepository archiveReadRepository,
    ToastService toastService
) : ComponentBase
{
    [Parameter]
    public int ArchiveId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private ArchiveDto Archive { get; set; } = null!;

    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        var archive = await archiveReadRepository.GetByIdAsync(ArchiveId);

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
